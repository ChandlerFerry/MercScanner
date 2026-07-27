using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using SharpDX;
using Vector2 = System.Numerics.Vector2;

namespace MercScanner;

public class MercScanner : BaseSettingsPlugin<MercScannerSettings>
{
    private static readonly string[] SkillNoise =
    [
        "Move",
        "EASMercenaryPortalOut",
    ];

    public override bool Initialise()
    {
        return true;
    }

    public override Job Tick()
    {
        return null;
    }

    public override void Render()
    {
        if (!Settings.IgnoreLargePanels && GameController.IngameState.IngameUi.LargePanels.Any(x => x.IsVisible) ||
            !Settings.IgnoreFullscreenPanels && GameController.IngameState.IngameUi.FullscreenPanels.Any(x => x.IsVisible))
        {
            return;
        }

        RenderIdleMercs();
        RenderValuableRucksackAlerts();
    }

    private void RenderIdleMercs()
    {
        foreach (var idleMerc in GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster]
                     .Where(x => x.Metadata.StartsWith("Metadata/Monsters/Mercenaries/", StringComparison.Ordinal) &&
                                 !x.IsHostile &&
                                 x.TryGetComponent<Positioned>(out var positioned) &&
                                 positioned.Reaction == 70))
        {
            if (!idleMerc.TryGetComponent<Actor>(out var actor))
                continue;

            var skills = CollectSkills(actor);
            if (skills.Count == 0)
                continue;

            var merScreenPos = GameController.IngameState.Camera.WorldToScreen(idleMerc.PosNum);
            var lineHeight = ImGui.GetTextLineHeight();
            var line = 0;

            var matchingSets = MercProfiles.SkillSets
                .Where(set => MercProfiles.MatchesType(idleMerc.Metadata, idleMerc.Path, idleMerc.RenderName, set)
                              && MercProfiles.IsFullMatch(skills, set))
                .ToList();

            if (Settings.ShowSetMatchBanner && matchingSets.Count > 0)
            {
                foreach (var set in matchingSets)
                {
                    var loadout = MercProfiles.GetMatchedLoadoutName(skills, set);
                    var banner = loadout != null
                        ? $"MATCH: {set.Name} ({loadout})"
                        : $"MATCH: {set.Name}";
                    Graphics.DrawTextWithBackground(
                        banner,
                        merScreenPos + new Vector2(0, lineHeight * line),
                        Settings.MatchColor,
                        Settings.BackgroundColor);
                    line++;
                }
            }

            foreach (var skill in skills)
            {
                var role = ClassifySkill(skill.Name);
                if (!Settings.ShowAllSkills && role == SkillRole.Normal)
                    continue;

                var color = role switch
                {
                    SkillRole.Required => matchingSets.Count > 0
                        ? Settings.MatchColor
                        : Settings.RequiredSkillColor,
                    SkillRole.Forbidden => Settings.ForbiddenSkillColor,
                    _ => Settings.DefaultSkillColor,
                };

                // Show +GMP3 / -GMP3 on actives that have per-skill link requirements.
                var label = skill.Name;
                var annotation = MercProfiles.SkillSets
                    .Select(set => MercProfiles.GetLinkAnnotation(skill.Name, skills, set))
                    .FirstOrDefault(a => a != null);
                if (annotation != null)
                {
                    label = $"{skill.Name} [{annotation}]";
                    if (annotation.Contains('-', StringComparison.Ordinal))
                        color = Settings.ForbiddenSkillColor;
                    else if (matchingSets.Count > 0)
                        color = Settings.MatchColor;
                    else
                        color = Settings.RequiredSkillColor;
                }

                Graphics.DrawTextWithBackground(
                    label,
                    merScreenPos + new Vector2(0, lineHeight * line),
                    color,
                    Settings.BackgroundColor);
                line++;
            }
        }
    }

    private void RenderValuableRucksackAlerts()
    {
        if (!Settings.AlertValuableItems)
            return;

        var window = GameController.IngameState.IngameUi.MercenaryEncounterWindow;
        if (window is not { IsVisible: true })
            return;

        var valuables = new List<(string Name, RectangleF Rect)>();

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

                if (!MercProfiles.IsValuableItem(baseName, renderName, metadata))
                    continue;

                var display = !string.IsNullOrWhiteSpace(baseName)
                    ? baseName
                    : !string.IsNullOrWhiteSpace(renderName)
                        ? renderName
                        : metadata;

                valuables.Add((display, invItem.GetClientRectCache));
            }
        }

        if (valuables.Count == 0)
            return;

        foreach (var (_, rect) in valuables)
        {
            Graphics.DrawFrame(rect, Settings.ValuableItemColor, 3);
        }

        var banner = "VALUABLE: " + string.Join(", ", valuables.Select(v => v.Name).Distinct(StringComparer.OrdinalIgnoreCase));
        var textSize = Graphics.MeasureText(banner);
        var screen = GameController.Window.GetWindowRectangleTimeCache;
        var pos = new Vector2(
            screen.Center.X - textSize.X / 2f,
            screen.Center.Y - screen.Height * 0.25f);

        Graphics.DrawTextWithBackground(banner, pos, Settings.ValuableItemColor, Settings.BackgroundColor);
    }

    private static List<MercSkillSnapshot> CollectSkills(Actor actor)
    {
        var skills = new List<MercSkillSnapshot>();
        foreach (var skill in actor.ActorSkills.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
        {
            var fullSkillName = skill.Name;
            if (SkillNoise.Any(n => fullSkillName.Equals(n, StringComparison.Ordinal)))
                continue;

            var skillName = fullSkillName;
            if (skillName.EndsWith("Mercenary", StringComparison.Ordinal))
                skillName = skillName[..^"Mercenary".Length];

            IReadOnlyDictionary<GameStat, int> stats;
            try
            {
                stats = skill.Stats ?? new Dictionary<GameStat, int>();
            }
            catch
            {
                stats = new Dictionary<GameStat, int>();
            }

            skills.Add(new MercSkillSnapshot(skillName, stats));
        }

        return skills;
    }

    private static SkillRole ClassifySkill(string skillName)
    {
        foreach (var set in MercProfiles.SkillSets)
        {
            if (set.ForbiddenSkills.Any(f => MercProfiles.SkillNameMatches(skillName, f)))
                return SkillRole.Forbidden;
            if (MercProfiles.IsRequiredSkillName(skillName, set))
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
}
