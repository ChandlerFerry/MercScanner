using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Helpers;
using SharpDX;
using Entity = ExileCore.PoEMemory.MemoryObjects.Entity;

namespace MercScanner;

public class MercScanner : BaseSettingsPlugin<MercScannerSettings>
{
    private static readonly int[] SkillListIndices = [2, 10, 0, 1, 0];

    // Rank 1 = best. Dark green (not lime), orange, blue.
    private static readonly Color Tier1Color = new(20, 110, 40, 255);
    private static readonly Color Tier2Color = new(220, 120, 20, 255);
    private static readonly Color Tier3Color = new(50, 120, 200, 255);
    private static readonly Color MatchColor = Tier1Color;
    private static readonly Color ValuableItemColor = Tier1Color;

    private bool _loggedMissingNinjaPrice;

    public override bool Initialise()
    {
        ReloadProfiles();
        return true;
    }

    private void ReloadProfiles()
    {
        if (MercProfiles.Load(DirectoryFullName))
        {
            LogMessage($"MercScanner: loaded {MercProfiles.SkillSets.Count} skill set(s) from {MercProfiles.LastLoadedPath}", 3);
            return;
        }

        if (!string.IsNullOrEmpty(ConfigDirectory) && MercProfiles.Load(ConfigDirectory))
        {
            LogMessage($"MercScanner: loaded {MercProfiles.SkillSets.Count} skill set(s) from {MercProfiles.LastLoadedPath}", 3);
            return;
        }

        LogError($"MercScanner: failed to load skill-sets.json — {MercProfiles.LastLoadError}");
    }

    public override Job Tick()
    {
        return null;
    }

    public override void Render()
    {
        var ingameUi = GameController.IngameState.IngameUi;

        if (!Settings.IgnoreFullscreenPanels && ingameUi.FullscreenPanels.Any(x => x.IsVisible))
            return;

        var window = ingameUi.MercenaryEncounterWindow;
        if (window is not { IsVisible: true, IsValid: true })
            return;

        var uiSkills = ReadEncounterSkills(window);
        var skills = uiSkills
            .Select(s => new MercSkillSnapshot(s.Name, s.Supports.Select(x => x.Name).ToList()))
            .ToList();
        var matches = MercProfiles.SkillSets
            .Select(set => MercProfiles.GetBestMatch(skills, set))
            .Where(m => m != null)
            .ToList();

        if (matches.Count > 0)
            DrawElementFrame(window.RematchButton, ColorForMatch(BestMatch(matches)));

        DrawSkillBorders(uiSkills, matches);
        RenderValuableRucksackAlerts(window);
    }

    private void DrawSkillBorders(List<UiSkill> uiSkills, List<MercMatch> matches)
    {
        foreach (var skill in uiSkills)
        {
            // Active skills only highlight on a set match (or as forbidden).
            // Partial "required" blue is for supports, not the skill name itself.
            if (TryColorForActive(skill.Name, matches, out var skillColor))
                DrawElementFrame(skill.NameElement, skillColor);

            foreach (var support in skill.Supports)
            {
                if (TryColorForSupport(skill.Name, support.Name, matches, out var supportColor))
                    DrawElementFrame(support.Slot, supportColor);
            }
        }
    }

    private bool TryColorForActive(string skillName, List<MercMatch> matches, out Color color)
    {
        var bestForSkill = matches
            .Where(m => MercProfiles.IsRequiredSkillName(skillName, m.Set))
            .OrderBy(m => m.Rank) // 1 = best
            .FirstOrDefault();

        if (bestForSkill != null)
        {
            color = ColorForMatch(bestForSkill);
            return true;
        }

        if (ClassifyActive(skillName) == SkillRole.Forbidden)
        {
            color = Settings.ForbiddenSkillColor;
            return true;
        }

        color = default;
        return false;
    }

    private bool TryColorForSupport(string activeSkillName, string supportName, List<MercMatch> matches, out Color color)
    {
        switch (ClassifySupport(activeSkillName, supportName))
        {
            case SkillRole.Required:
                color = matches.Count > 0
                    ? ColorForMatch(BestMatch(matches))
                    : Settings.RequiredSkillColor;
                return true;
            case SkillRole.Forbidden:
                color = Settings.ForbiddenSkillColor;
                return true;
            default:
                color = default;
                return false;
        }
    }

    private static MercMatch BestMatch(List<MercMatch> matches) =>
        matches.OrderBy(m => m.Rank).First(); // 1 = best

    private static Color ColorForMatch(MercMatch match)
    {
        if (match == null)
            return MatchColor;

        // Single-band sets (e.g. Sniper Full) use best-tier green.
        if (match.Set.Tiers.Count <= 1)
            return MatchColor;

        return match.Rank switch
        {
            1 => Tier1Color,
            2 => Tier2Color,
            3 => Tier3Color,
            _ => MatchColor,
        };
    }

    private void RenderValuableRucksackAlerts(MercenaryEncounterWindow window)
    {
        var minChaos = Settings.AlertMinChaosValue.Value;
        var valuables = new List<(string Name, double Chaos, RectangleF Rect)>();

        foreach (var inventory in window.Inventories ?? Enumerable.Empty<VendorInventory>())
        {
            var items = inventory?.VisibleInventoryItems;
            if (items == null)
                continue;

            foreach (var invItem in items)
            {
                var entity = invItem?.Item;
                if (entity is not { Address: not 0, IsValid: true })
                    continue;

                var baseName = entity.TryGetComponent<Base>(out var baseComp) ? baseComp?.Name : null;
                var renderName = entity.RenderName;
                var metadata = entity.Metadata;

                var display = !string.IsNullOrWhiteSpace(baseName)
                    ? baseName
                    : !string.IsNullOrWhiteSpace(renderName)
                        ? renderName
                        : metadata ?? "?";

                var chaos = TryGetNinjaChaosValue(entity);
                if (chaos + 1e-6 < minChaos)
                    continue;

                valuables.Add((display, chaos, invItem.GetClientRectCache));
            }
        }

        if (valuables.Count == 0)
            return;

        foreach (var (_, _, rect) in valuables)
        {
            Graphics.DrawFrame(rect, ValuableItemColor, 3);
        }

        DrawElementFrame(window.TakeItemButton, ValuableItemColor);
    }

    private void DrawElementFrame(Element element, Color color)
    {
        try
        {
            if (element is not { IsValid: true, Address: not 0, IsVisible: true })
                return;

            var rect = element.GetClientRectCache;
            if (rect.Width <= 1 || rect.Height <= 1)
                return;

            Graphics.DrawFrame(rect, color, 3);
        }
        catch
        {
        }
    }

    private double TryGetNinjaChaosValue(Entity item)
    {
        try
        {
            if (item == null)
                return 0;

            var getValue = GameController.PluginBridge.GetMethod<Func<Entity, double>>("NinjaPrice.GetValue");
            if (getValue == null)
            {
                if (!_loggedMissingNinjaPrice)
                {
                    _loggedMissingNinjaPrice = true;
                    LogError("NinjaPrice.GetValue method not found — enable Ninja Price for chaos-value merc alerts");
                }

                return 0;
            }

            var value = getValue(item);
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                return 0;

            return value;
        }
        catch (Exception ex)
        {
            LogError($"Error getting item value from NinjaPrice: {ex.Message}");
            return 0;
        }
    }

    private static List<UiSkill> ReadEncounterSkills(MercenaryEncounterWindow window)
    {
        var result = new List<UiSkill>();

        Element skillsRoot;
        try
        {
            skillsRoot = window.GetChildFromIndices(SkillListIndices);
        }
        catch
        {
            return result;
        }

        if (skillsRoot is not { IsValid: true, Address: not 0 })
            return result;

        IList<Element> skillLines;
        try
        {
            skillLines = skillsRoot.Children;
        }
        catch
        {
            return result;
        }

        if (skillLines == null)
            return result;

        foreach (var skillLine in skillLines)
        {
            if (skillLine is not { IsValid: true, Address: not 0 })
                continue;

            Element nameEl;
            string skillName;
            try
            {
                nameEl = skillLine.GetChildAtIndex(1) ?? skillLine[1];
                skillName = CleanUiText(nameEl?.Text);
            }
            catch
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(skillName) || nameEl is not { IsValid: true, Address: not 0 })
                continue;

            result.Add(new UiSkill(skillName, nameEl, ReadSupports(skillLine)));
        }

        return result;
    }

    private static List<UiSupport> ReadSupports(Element skillLine)
    {
        var supports = new List<UiSupport>();

        Element supportList;
        try
        {
            supportList = skillLine.GetChildFromIndices(3, 0);
        }
        catch
        {
            return supports;
        }

        if (supportList is not { IsValid: true, Address: not 0 })
            return supports;

        IList<Element> supportSlots;
        try
        {
            supportSlots = supportList.Children;
        }
        catch
        {
            return supports;
        }

        if (supportSlots == null)
            return supports;

        foreach (var slot in supportSlots)
        {
            if (slot is not { IsValid: true, Address: not 0 })
                continue;

            var name = ReadSupportName(slot);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            supports.Add(new UiSupport(name, slot));
        }

        return supports;
    }

    private static string ReadSupportName(Element supportSlot)
    {
        try
        {
            var tooltip = supportSlot.Tooltip;
            if (tooltip is not { IsValid: true, Address: not 0 })
                return null;

            var nameEl = tooltip.GetChildFromIndices(0, 0) ?? tooltip[0]?[0];
            var text = CleanUiText(nameEl?.Text);
            if (!string.IsNullOrWhiteSpace(text))
                return text;

            return CleanUiText(tooltip.Text);
        }
        catch
        {
            return null;
        }
    }

    private static string CleanUiText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        text = text.Trim();
        var newline = text.IndexOfAny(['\r', '\n']);
        if (newline >= 0)
            text = text[..newline].Trim();

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static SkillRole ClassifyActive(string skillName)
    {
        foreach (var set in MercProfiles.SkillSets)
        {
            if (MercProfiles.IsForbiddenSkillName(skillName, set))
                return SkillRole.Forbidden;
            if (MercProfiles.IsRequiredSkillName(skillName, set))
                return SkillRole.Required;
        }

        return SkillRole.Normal;
    }

    private static SkillRole ClassifySupport(string activeSkillName, string supportName)
    {
        foreach (var set in MercProfiles.SkillSets)
        {
            if (MercProfiles.IsForbiddenSupportOnActive(activeSkillName, supportName, set))
                return SkillRole.Forbidden;
            if (MercProfiles.IsRequiredSupportOnActive(activeSkillName, supportName, set))
                return SkillRole.Required;
        }

        return SkillRole.Normal;
    }

    private enum SkillRole
    {
        Normal,
        Required,
        Forbidden,
    }

    private sealed class UiSkill(string name, Element nameElement, List<UiSupport> supports)
    {
        public string Name { get; } = name;
        public Element NameElement { get; } = nameElement;
        public List<UiSupport> Supports { get; } = supports ?? [];
    }

    private sealed class UiSupport(string name, Element slot)
    {
        public string Name { get; } = name;
        public Element Slot { get; } = slot;
    }
}
