using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ExileCore.Shared.Enums;
using Newtonsoft.Json;

namespace MercScanner;

public sealed class MercSkillSnapshot(string name, IReadOnlyList<string> supports = null)
{
    public string Name { get; } = name ?? "";
    public IReadOnlyList<string> Supports { get; } = supports ?? [];
    public IReadOnlyDictionary<GameStat, int> Stats { get; } = new Dictionary<GameStat, int>();

    public int GetStat(GameStat stat) =>
        Stats.TryGetValue(stat, out var value) ? value : 0;
}

public sealed class LinkedSupportRequirement
{
    public LinkedSupportRequirement(string activeSkill, params string[] supports)
    {
        ActiveSkill = activeSkill;
        Supports = supports ?? [];
    }

    public LinkedSupportRequirement(string activeSkill, IReadOnlyList<string> supports)
    {
        ActiveSkill = activeSkill;
        Supports = supports ?? [];
    }

    public string ActiveSkill { get; }
    public IReadOnlyList<string> Supports { get; }
}

public sealed class MercLoadout(
    string name,
    IReadOnlyList<LinkedSupportRequirement> requiredLinks = null,
    IReadOnlyList<IReadOnlyList<string>> requiredAnyOfGroups = null)
{
    public string Name { get; } = name;
    public IReadOnlyList<LinkedSupportRequirement> RequiredLinks { get; } = requiredLinks ?? [];
    public IReadOnlyList<IReadOnlyList<string>> RequiredAnyOfGroups { get; } = requiredAnyOfGroups ?? [];
}

public sealed class MercSkillSet(
    string name,
    IReadOnlyList<string> requiredSkills = null,
    IReadOnlyList<string> forbiddenSkills = null,
    IReadOnlyList<IReadOnlyList<string>> requiredAnyOfGroups = null,
    IReadOnlyList<LinkedSupportRequirement> requiredLinks = null,
    IReadOnlyList<MercLoadout> requiredAnyLoadout = null,
    IReadOnlyList<string> typeMatchers = null)
{
    public string Name { get; } = name;
    public IReadOnlyList<string> TypeMatchers { get; } = typeMatchers ?? [];
    public IReadOnlyList<string> RequiredSkills { get; } = requiredSkills ?? [];
    public IReadOnlyList<IReadOnlyList<string>> RequiredAnyOfGroups { get; } = requiredAnyOfGroups ?? [];
    public IReadOnlyList<LinkedSupportRequirement> RequiredLinks { get; } = requiredLinks ?? [];
    public IReadOnlyList<MercLoadout> RequiredAnyLoadout { get; } = requiredAnyLoadout ?? [];
    public IReadOnlyList<string> ForbiddenSkills { get; } = forbiddenSkills ?? [];
}

public static class MercProfiles
{
    public const string SkillSetsFileName = "skill-sets.json";

    public static IReadOnlyList<MercSkillSet> SkillSets { get; private set; } = [];
    public static string LastLoadError { get; private set; }
    public static string LastLoadedPath { get; private set; }

    public static bool Load(string directoryOrFilePath)
    {
        LastLoadError = null;
        LastLoadedPath = null;

        try
        {
            var path = ResolvePath(directoryOrFilePath);
            if (path == null || !File.Exists(path))
            {
                LastLoadError = $"skill-sets.json not found (looked under: {directoryOrFilePath})";
                SkillSets = [];
                return false;
            }

            var json = File.ReadAllText(path);
            var dto = JsonConvert.DeserializeObject<SkillSetsFileDto>(json);
            if (dto == null)
            {
                LastLoadError = "skill-sets.json deserialized to null";
                SkillSets = [];
                return false;
            }

            SkillSets = [.. (dto.SkillSets ?? [])
                .Where(s => s != null && !string.IsNullOrWhiteSpace(s.Name))
                .Select(MapSkillSet)];

            LastLoadedPath = path;
            return true;
        }
        catch (Exception ex)
        {
            LastLoadError = ex.Message;
            SkillSets = [];
            return false;
        }
    }

    private static string ResolvePath(string directoryOrFilePath)
    {
        if (string.IsNullOrWhiteSpace(directoryOrFilePath))
            return null;

        if (File.Exists(directoryOrFilePath) &&
            directoryOrFilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return directoryOrFilePath;

        var candidate = Path.Combine(directoryOrFilePath, SkillSetsFileName);
        return File.Exists(candidate) ? candidate : candidate;
    }

    private static MercSkillSet MapSkillSet(SkillSetDto dto)
    {
        return new MercSkillSet(
            name: dto.Name,
            requiredSkills: dto.RequiredSkills,
            forbiddenSkills: dto.ForbiddenSkills,
            requiredAnyOfGroups: MapAnyOfGroups(dto.RequiredAnyOfGroups),
            requiredLinks: MapLinks(dto.RequiredLinks),
            requiredAnyLoadout: [.. (dto.RequiredAnyLoadout ?? [])
                .Where(l => l != null && !string.IsNullOrWhiteSpace(l.Name))
                .Select(l => new MercLoadout(
                    l.Name,
                    MapLinks(l.RequiredLinks),
                    MapAnyOfGroups(l.RequiredAnyOfGroups)))],
            typeMatchers: dto.TypeMatchers);
    }

    private static IReadOnlyList<LinkedSupportRequirement> MapLinks(List<LinkDto> links)
    {
        return [.. (links ?? [])
            .Where(l => l != null && !string.IsNullOrWhiteSpace(l.ActiveSkill))
            .Select(l => new LinkedSupportRequirement(l.ActiveSkill, l.Supports ?? []))];
    }

    private static IReadOnlyList<IReadOnlyList<string>> MapAnyOfGroups(List<List<string>> groups)
    {
        return [.. (groups ?? [])
            .Where(g => g != null && g.Count > 0)
            .Select(g => (IReadOnlyList<string>)g)];
    }

    #region JSON DTOs

    private sealed class SkillSetsFileDto
    {
        [JsonProperty("skillSets")]
        public List<SkillSetDto> SkillSets { get; set; }
    }

    private sealed class SkillSetDto
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("typeMatchers")]
        public List<string> TypeMatchers { get; set; }

        [JsonProperty("requiredSkills")]
        public List<string> RequiredSkills { get; set; }

        [JsonProperty("requiredLinks")]
        public List<LinkDto> RequiredLinks { get; set; }

        [JsonProperty("requiredAnyOfGroups")]
        public List<List<string>> RequiredAnyOfGroups { get; set; }

        [JsonProperty("requiredAnyLoadout")]
        public List<LoadoutDto> RequiredAnyLoadout { get; set; }

        [JsonProperty("forbiddenSkills")]
        public List<string> ForbiddenSkills { get; set; }
    }

    private sealed class LoadoutDto
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("requiredLinks")]
        public List<LinkDto> RequiredLinks { get; set; }

        [JsonProperty("requiredAnyOfGroups")]
        public List<List<string>> RequiredAnyOfGroups { get; set; }
    }

    private sealed class LinkDto
    {
        [JsonProperty("activeSkill")]
        public string ActiveSkill { get; set; }

        [JsonProperty("supports")]
        public List<string> Supports { get; set; }
    }

    #endregion

    public static bool SkillNameMatches(string skillName, string pattern)
    {
        if (string.IsNullOrWhiteSpace(skillName) || string.IsNullOrWhiteSpace(pattern))
            return false;

        var left = NormalizeSkillName(skillName);
        var right = NormalizeSkillName(pattern);

        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            return false;

        if (left.Equals(right, StringComparison.InvariantCultureIgnoreCase))
            return true;

        if (left.StartsWith(right + " of ", StringComparison.InvariantCultureIgnoreCase))
            return true;

        foreach (var prefix in new[] { "Greater ", "Lesser ", "Vaal ", "Awakened ", "Gilded " })
        {
            if (!left.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase) ||
                right.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase))
                continue;

            var rest = left[prefix.Length..];
            if (rest.Equals(right, StringComparison.InvariantCultureIgnoreCase))
                return false;
            if (rest.StartsWith(right + " of ", StringComparison.InvariantCultureIgnoreCase))
                return false;
        }

        return left.Contains(right, StringComparison.InvariantCultureIgnoreCase);
    }

    public static string NormalizeSkillName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";

        var text = name.Trim();

        if (text.EndsWith(" Support", StringComparison.InvariantCultureIgnoreCase))
            text = text[..^" Support".Length].TrimEnd();

        text = Regex.Replace(
            text,
            @"\s+(V|IV|III|II|I)$",
            "",
            RegexOptions.IgnoreCase);

        return text.Trim();
    }

    public static bool HasSkill(IEnumerable<string> skillNames, string pattern)
    {
        return skillNames.Any(s => SkillNameMatches(s, pattern));
    }

    public static bool HasSkillOrSupport(IReadOnlyList<MercSkillSnapshot> skills, string pattern)
    {
        return skills.Any(s =>
            SkillNameMatches(s.Name, pattern) ||
            s.Supports.Any(sup => SkillNameMatches(sup, pattern)));
    }

    public static MercSkillSnapshot FindSkill(IReadOnlyList<MercSkillSnapshot> skills, string pattern)
    {
        return skills.FirstOrDefault(s => SkillNameMatches(s.Name, pattern));
    }

    public static bool MatchesType(string metadata, string path, string renderName, MercSkillSet set)
    {
        if (set.TypeMatchers.Count == 0)
            return true;

        foreach (var matcher in set.TypeMatchers)
        {
            if (string.IsNullOrWhiteSpace(matcher))
                continue;

            if (!string.IsNullOrEmpty(metadata) &&
                metadata.Contains(matcher, StringComparison.InvariantCultureIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(path) &&
                path.Contains(matcher, StringComparison.InvariantCultureIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(renderName) &&
                renderName.Contains(matcher, StringComparison.InvariantCultureIgnoreCase))
                return true;
        }

        return false;
    }

    public static bool ActiveSkillHasSupport(
        MercSkillSnapshot skill,
        string supportName,
        IReadOnlyList<MercSkillSnapshot> allSkills)
    {
        if (skill == null || string.IsNullOrWhiteSpace(supportName))
            return false;

        if (skill.Supports.Any(s => SkillNameMatches(s, supportName)))
            return true;

        _ = allSkills;
        return false;
    }

    public static string ShortSupportName(string supportName)
    {
        var n = NormalizeSkillName(supportName);
        if (n.Contains("Greater Multiple Projectiles", StringComparison.OrdinalIgnoreCase)
            || n.Equals("GMP", StringComparison.OrdinalIgnoreCase))
            return "GMP";
        if (n.Equals("Return", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("Return ", StringComparison.OrdinalIgnoreCase))
            return "Return";
        if (n.Contains("Gilded Secondary Shots", StringComparison.OrdinalIgnoreCase))
            return "GildSec";
        if (n.Contains("Gilded Molten Eruption", StringComparison.OrdinalIgnoreCase))
            return "GildErupt";
        if (n.Contains("Greater Elemental Damage with Attacks", StringComparison.OrdinalIgnoreCase)
            || n.Equals("WED", StringComparison.OrdinalIgnoreCase))
            return "WED";
        if (n.Contains("Hypothermia", StringComparison.OrdinalIgnoreCase))
            return "Hypo";
        if (n.Contains("Cooldown Recovery", StringComparison.OrdinalIgnoreCase))
            return "CDR";
        if (n.Contains("More Duration", StringComparison.OrdinalIgnoreCase))
            return "MoreDur";
        if (n.Contains("Multistrike", StringComparison.OrdinalIgnoreCase))
            return "Multi";
        if (n.Contains("Pierce", StringComparison.OrdinalIgnoreCase)
            && n.Contains("Greater", StringComparison.OrdinalIgnoreCase))
            return "Pierce";
        if (n.Contains("Chain", StringComparison.OrdinalIgnoreCase))
            return "Chain";
        if (n.Contains("Fork", StringComparison.OrdinalIgnoreCase))
            return "Fork";
        if (n.Contains("Multiple Traps", StringComparison.OrdinalIgnoreCase))
            return "MultiTrap";
        if (n.Contains("Throwing Speed", StringComparison.OrdinalIgnoreCase))
            return "ThrowSpd";
        if (n.Contains("Melee Physical Damage", StringComparison.OrdinalIgnoreCase))
            return "MeleePhys";
        if (n.Contains("Brutality", StringComparison.OrdinalIgnoreCase))
            return "Brut";
        if (n.Contains("Arrow Nova", StringComparison.OrdinalIgnoreCase))
            return "ArrowNova";
        return supportName;
    }

    public static bool IsFullMatch(IReadOnlyList<MercSkillSnapshot> skills, MercSkillSet set)
    {
        if (set.RequiredSkills.Count == 0 &&
            set.RequiredAnyOfGroups.Count == 0 &&
            set.RequiredLinks.Count == 0 &&
            set.RequiredAnyLoadout.Count == 0 &&
            set.ForbiddenSkills.Count == 0)
            return false;

        foreach (var forbidden in set.ForbiddenSkills)
        {
            if (HasSkillOrSupport(skills, forbidden))
                return false;
        }

        foreach (var required in set.RequiredSkills)
        {
            if (!HasSkillOrSupport(skills, required))
                return false;
        }

        foreach (var group in set.RequiredAnyOfGroups)
        {
            if (group == null || group.Count == 0)
                continue;

            if (!group.Any(option => HasSkillOrSupport(skills, option)))
                return false;
        }

        foreach (var link in set.RequiredLinks)
        {
            if (!LinkRequirementMet(skills, link))
                return false;
        }

        if (set.RequiredAnyLoadout.Count > 0)
        {
            if (!set.RequiredAnyLoadout.Any(loadout => LoadoutMatches(skills, loadout)))
                return false;
        }

        return true;
    }

    public static string GetMatchedLoadoutName(IReadOnlyList<MercSkillSnapshot> skills, MercSkillSet set)
    {
        foreach (var loadout in set.RequiredAnyLoadout)
        {
            if (LoadoutMatches(skills, loadout))
                return loadout.Name;
        }

        return null;
    }

    public static bool LoadoutMatches(IReadOnlyList<MercSkillSnapshot> skills, MercLoadout loadout)
    {
        if (loadout == null)
            return false;

        foreach (var link in loadout.RequiredLinks)
        {
            if (!LinkRequirementMet(skills, link))
                return false;
        }

        foreach (var group in loadout.RequiredAnyOfGroups)
        {
            if (group == null || group.Count == 0)
                continue;

            if (!group.Any(option => HasSkillOrSupport(skills, option)))
                return false;
        }

        return loadout.RequiredLinks.Count > 0 || loadout.RequiredAnyOfGroups.Count > 0;
    }

    public static bool LinkRequirementMet(IReadOnlyList<MercSkillSnapshot> skills, LinkedSupportRequirement link)
    {
        var active = FindSkill(skills, link.ActiveSkill);
        if (active == null)
            return false;

        foreach (var support in link.Supports)
        {
            if (!ActiveSkillHasSupport(active, support, skills))
                return false;
        }

        return true;
    }

    public static bool IsRequiredSkillName(string skillName, MercSkillSet set)
    {
        if (set.RequiredSkills.Any(r => SkillNameMatches(skillName, r)))
            return true;

        if (set.RequiredLinks.Any(l => SkillNameMatches(skillName, l.ActiveSkill)
                                       || l.Supports.Any(s => SkillNameMatches(skillName, s))))
            return true;

        foreach (var group in set.RequiredAnyOfGroups)
        {
            if (group != null && group.Any(option => SkillNameMatches(skillName, option)))
                return true;
        }

        foreach (var loadout in set.RequiredAnyLoadout)
        {
            if (loadout.RequiredLinks.Any(l => SkillNameMatches(skillName, l.ActiveSkill)
                                               || l.Supports.Any(s => SkillNameMatches(skillName, s))))
                return true;

            foreach (var group in loadout.RequiredAnyOfGroups)
            {
                if (group != null && group.Any(option => SkillNameMatches(skillName, option)))
                    return true;
            }
        }

        return false;
    }

    public static string GetLinkAnnotation(string skillName, IReadOnlyList<MercSkillSnapshot> skills, MercSkillSet set)
    {
        var notes = new List<string>();
        var links = set.RequiredLinks
            .Concat(set.RequiredAnyLoadout.SelectMany(l => l.RequiredLinks));

        foreach (var link in links)
        {
            if (!SkillNameMatches(skillName, link.ActiveSkill))
                continue;

            var active = FindSkill(skills, link.ActiveSkill);
            foreach (var support in link.Supports)
            {
                var shortName = ShortSupportName(support);
                if (active != null && ActiveSkillHasSupport(active, support, skills))
                    notes.Add($"+{shortName}");
                else
                    notes.Add($"-{shortName}");
            }
        }

        return notes.Count == 0 ? null : string.Join(" ", notes);
    }
}
