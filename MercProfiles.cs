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
    public LinkedSupportRequirement(
        string activeSkill,
        IReadOnlyList<string> supports = null,
        IReadOnlyList<string> forbiddenSupports = null)
    {
        ActiveSkill = activeSkill;
        Supports = supports ?? [];
        ForbiddenSupports = forbiddenSupports ?? [];
    }

    public string ActiveSkill { get; }
    public IReadOnlyList<string> Supports { get; }
    public IReadOnlyList<string> ForbiddenSupports { get; }
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

/// <summary>
/// One quality band for a skill set. Tiers are ordered best→worst in JSON;
/// rank is 1-based index (1 = best). First matching tier wins.
/// </summary>
public sealed class MercTierSpec(
    string name,
    IReadOnlyList<LinkedSupportRequirement> requiredLinks = null,
    IReadOnlyList<IReadOnlyList<string>> requiredAnyOfGroups = null,
    IReadOnlyList<string> requiredSkills = null,
    IReadOnlyList<MercLoadout> requiredAnyLoadout = null,
    IReadOnlyList<string> forbiddenSkills = null)
{
    public string Name { get; } = name;
    public IReadOnlyList<string> RequiredSkills { get; } = requiredSkills ?? [];
    public IReadOnlyList<LinkedSupportRequirement> RequiredLinks { get; } = requiredLinks ?? [];
    public IReadOnlyList<IReadOnlyList<string>> RequiredAnyOfGroups { get; } = requiredAnyOfGroups ?? [];
    public IReadOnlyList<MercLoadout> RequiredAnyLoadout { get; } = requiredAnyLoadout ?? [];
    /// <summary>Extra forbids beyond set-level (e.g. Mirror Arrow only on Better).</summary>
    public IReadOnlyList<string> ForbiddenSkills { get; } = forbiddenSkills ?? [];
}

public sealed class MercMatch(MercSkillSet set, int rank, string tierName, string loadoutName = null)
{
    public MercSkillSet Set { get; } = set;
    /// <summary>1 = best, higher = worse.</summary>
    public int Rank { get; } = rank;
    public string TierName { get; } = tierName ?? "";
    public string LoadoutName { get; } = loadoutName;
    public string DisplayName
    {
        get
        {
            var baseName = string.IsNullOrWhiteSpace(TierName)
                ? Set.Name
                : $"{Set.Name} ({TierName})";
            return string.IsNullOrWhiteSpace(LoadoutName)
                ? baseName
                : $"{baseName} [{LoadoutName}]";
        }
    }
}

public sealed class MercSkillSet(
    string name,
    IReadOnlyList<MercTierSpec> tiers = null,
    IReadOnlyList<string> requiredSkills = null,
    IReadOnlyList<string> forbiddenSkills = null,
    IReadOnlyList<string> typeMatchers = null)
{
    public string Name { get; } = name;
    public IReadOnlyList<string> TypeMatchers { get; } = typeMatchers ?? [];
    /// <summary>Actives required on every tier (optional convenience).</summary>
    public IReadOnlyList<string> RequiredSkills { get; } = requiredSkills ?? [];
    public IReadOnlyList<string> ForbiddenSkills { get; } = forbiddenSkills ?? [];
    public IReadOnlyList<MercTierSpec> Tiers { get; } = tiers ?? [];
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
            tiers: MapTiers(dto.Tiers),
            requiredSkills: dto.RequiredSkills,
            forbiddenSkills: dto.ForbiddenSkills,
            typeMatchers: dto.TypeMatchers);
    }

    private static IReadOnlyList<MercTierSpec> MapTiers(List<TierDto> tiers)
    {
        return [.. (tiers ?? [])
            .Where(t => t != null)
            .Select((t, i) => new MercTierSpec(
                name: string.IsNullOrWhiteSpace(t.Name) ? DefaultTierName(i + 1) : t.Name.Trim(),
                requiredLinks: MapLinks(t.RequiredLinks),
                requiredAnyOfGroups: MapAnyOfGroups(t.RequiredAnyOfGroups),
                requiredSkills: t.RequiredSkills,
                requiredAnyLoadout: MapLoadouts(t.RequiredAnyLoadout),
                forbiddenSkills: t.ForbiddenSkills))];
    }

    private static IReadOnlyList<MercLoadout> MapLoadouts(List<LoadoutDto> loadouts)
    {
        return [.. (loadouts ?? [])
            .Where(l => l != null && !string.IsNullOrWhiteSpace(l.Name))
            .Select(l => new MercLoadout(
                l.Name,
                MapLinks(l.RequiredLinks),
                MapAnyOfGroups(l.RequiredAnyOfGroups)))];
    }

    private static string DefaultTierName(int rank) => rank switch
    {
        1 => "Best",
        2 => "Better",
        3 => "Sellable",
        _ => $"T{rank}",
    };

    private static IReadOnlyList<LinkedSupportRequirement> MapLinks(List<LinkDto> links)
    {
        return [.. (links ?? [])
            .Where(l => l != null && !string.IsNullOrWhiteSpace(l.ActiveSkill))
            .Select(l => new LinkedSupportRequirement(
                l.ActiveSkill,
                l.Supports ?? [],
                l.ForbiddenSupports ?? []))];
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

        [JsonProperty("tiers")]
        public List<TierDto> Tiers { get; set; }

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

    private sealed class TierDto
    {
        [JsonProperty("name")]
        public string Name { get; set; }

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

    private sealed class LinkDto
    {
        [JsonProperty("activeSkill")]
        public string ActiveSkill { get; set; }

        [JsonProperty("supports")]
        public List<string> Supports { get; set; }

        [JsonProperty("forbiddenSupports")]
        public List<string> ForbiddenSupports { get; set; }
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

        // Exact name only (after strip of " Support" / Roman tiers). No "of" variants, prefixes, or substring matches.
        return left.Equals(right, StringComparison.InvariantCultureIgnoreCase);
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

    public static IEnumerable<string> RelevantActivePatterns(MercSkillSet set)
    {
        foreach (var required in set.RequiredSkills)
            yield return required;

        foreach (var tier in set.Tiers)
        {
            foreach (var pattern in RelevantActivePatterns(tier))
                yield return pattern;
        }
    }

    public static IEnumerable<string> RelevantActivePatterns(MercTierSpec tier)
    {
        foreach (var required in tier.RequiredSkills)
            yield return required;

        foreach (var link in tier.RequiredLinks)
            yield return link.ActiveSkill;

        foreach (var loadout in tier.RequiredAnyLoadout)
        {
            foreach (var pattern in RelevantActivePatterns(loadout))
                yield return pattern;
        }
    }

    public static IEnumerable<string> RelevantActivePatterns(MercLoadout loadout)
    {
        foreach (var link in loadout.RequiredLinks)
            yield return link.ActiveSkill;
    }

    public static bool HasOptionOnActives(
        IReadOnlyList<MercSkillSnapshot> skills,
        IEnumerable<string> activePatterns,
        string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return false;

        var actives = activePatterns?.Where(a => !string.IsNullOrWhiteSpace(a)).ToList() ?? [];

        foreach (var skill in skills)
        {
            if (SkillNameMatches(skill.Name, pattern))
                return true;

            if (actives.Count == 0)
                continue;

            if (!actives.Any(a => SkillNameMatches(skill.Name, a)))
                continue;

            if (skill.Supports.Any(sup => SkillNameMatches(sup, pattern)))
                return true;
        }

        return false;
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
        if (n.Contains("Increased Area of Effect", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Area of Effect", StringComparison.OrdinalIgnoreCase))
            return "AoE";
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
        if (n.Contains("Slower Projectiles", StringComparison.OrdinalIgnoreCase))
            return "SlowProj";
        if (n.Contains("Faster Projectiles", StringComparison.OrdinalIgnoreCase))
            return "FastProj";
        return supportName;
    }

    /// <summary>
    /// Best tier that matches. Tiers are ordered best→worst; first match wins (rank 1 = best).
    /// </summary>
    public static MercMatch GetBestMatch(IReadOnlyList<MercSkillSnapshot> skills, MercSkillSet set)
    {
        if (set == null || set.Tiers.Count == 0)
            return null;

        if (HasAnyForbidden(skills, set.ForbiddenSkills))
            return null;

        for (var i = 0; i < set.Tiers.Count; i++)
        {
            var tier = set.Tiers[i];
            if (!TierMatches(skills, set, tier, out var loadoutName))
                continue;

            return new MercMatch(set, i + 1, tier.Name, loadoutName);
        }

        return null;
    }

    public static bool IsFullMatch(IReadOnlyList<MercSkillSnapshot> skills, MercSkillSet set) =>
        GetBestMatch(skills, set) != null;

    private static bool HasAnyForbidden(IReadOnlyList<MercSkillSnapshot> skills, IReadOnlyList<string> forbidden)
    {
        if (forbidden == null || forbidden.Count == 0)
            return false;

        foreach (var name in forbidden)
        {
            if (HasSkill(skills.Select(s => s.Name), name))
                return true;
        }

        return false;
    }

    private static bool TierMatches(
        IReadOnlyList<MercSkillSnapshot> skills,
        MercSkillSet set,
        MercTierSpec tier,
        out string loadoutName)
    {
        loadoutName = null;
        if (tier == null)
            return false;

        if (HasAnyForbidden(skills, tier.ForbiddenSkills))
            return false;

        foreach (var required in set.RequiredSkills)
        {
            if (!HasSkill(skills.Select(s => s.Name), required))
                return false;
        }

        foreach (var required in tier.RequiredSkills)
        {
            if (!HasSkill(skills.Select(s => s.Name), required))
                return false;
        }

        foreach (var link in tier.RequiredLinks)
        {
            if (!LinkRequirementMet(skills, link))
                return false;
        }

        var actives = RelevantActivePatterns(set)
            .Concat(RelevantActivePatterns(tier))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in tier.RequiredAnyOfGroups)
        {
            if (group == null || group.Count == 0)
                continue;

            if (!group.Any(option => HasOptionOnActives(skills, actives, option)))
                return false;
        }

        if (tier.RequiredAnyLoadout.Count > 0)
        {
            MercLoadout matched = null;
            foreach (var loadout in tier.RequiredAnyLoadout)
            {
                if (!LoadoutMatches(skills, loadout))
                    continue;
                matched = loadout;
                break;
            }

            if (matched == null)
                return false;

            loadoutName = matched.Name;
        }

        return tier.RequiredLinks.Count > 0
               || tier.RequiredAnyOfGroups.Count > 0
               || tier.RequiredSkills.Count > 0
               || tier.RequiredAnyLoadout.Count > 0
               || set.RequiredSkills.Count > 0;
    }

    public static string GetMatchedLoadoutName(IReadOnlyList<MercSkillSnapshot> skills, MercSkillSet set)
    {
        return GetBestMatch(skills, set)?.LoadoutName;
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

        var loadoutActives = RelevantActivePatterns(loadout).ToList();
        foreach (var group in loadout.RequiredAnyOfGroups)
        {
            if (group == null || group.Count == 0)
                continue;

            if (!group.Any(option => HasOptionOnActives(skills, loadoutActives, option)))
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

        foreach (var forbidden in link.ForbiddenSupports)
        {
            if (ActiveSkillHasSupport(active, forbidden, skills))
                return false;
        }

        return true;
    }

    public static bool IsRequiredSkillName(string skillName, MercSkillSet set)
    {
        if (set.RequiredSkills.Any(r => SkillNameMatches(skillName, r)))
            return true;

        foreach (var tier in set.Tiers)
        {
            if (tier.RequiredSkills.Any(r => SkillNameMatches(skillName, r)))
                return true;

            if (tier.RequiredLinks.Any(l => SkillNameMatches(skillName, l.ActiveSkill)))
                return true;

            foreach (var loadout in tier.RequiredAnyLoadout)
            {
                if (loadout.RequiredLinks.Any(l => SkillNameMatches(skillName, l.ActiveSkill)))
                    return true;
            }
        }

        return false;
    }

    public static bool IsRequiredSupportOnActive(string activeSkillName, string supportName, MercSkillSet set)
    {
        if (string.IsNullOrWhiteSpace(activeSkillName) || string.IsNullOrWhiteSpace(supportName))
            return false;

        var setActives = RelevantActivePatterns(set).ToList();

        foreach (var tier in set.Tiers)
        {
            if (LinkListHasRequiredSupport(tier.RequiredLinks, activeSkillName, supportName))
                return true;

            var tierActives = RelevantActivePatterns(tier).ToList();
            if (set.RequiredSkills.Any(r => SkillNameMatches(activeSkillName, r)) ||
                tierActives.Any(a => SkillNameMatches(activeSkillName, a)) ||
                setActives.Any(a => SkillNameMatches(activeSkillName, a)))
            {
                foreach (var group in tier.RequiredAnyOfGroups)
                {
                    if (group != null && group.Any(option => SkillNameMatches(supportName, option)))
                        return true;
                }
            }

            foreach (var loadout in tier.RequiredAnyLoadout)
            {
                if (LinkListHasRequiredSupport(loadout.RequiredLinks, activeSkillName, supportName))
                    return true;

                var loadoutActives = RelevantActivePatterns(loadout).ToList();
                if (!loadoutActives.Any(a => SkillNameMatches(activeSkillName, a)))
                    continue;

                foreach (var group in loadout.RequiredAnyOfGroups)
                {
                    if (group != null && group.Any(option => SkillNameMatches(supportName, option)))
                        return true;
                }
            }
        }

        return false;
    }

    private static bool LinkListHasRequiredSupport(
        IReadOnlyList<LinkedSupportRequirement> links,
        string activeSkillName,
        string supportName)
    {
        foreach (var link in links)
        {
            if (!SkillNameMatches(activeSkillName, link.ActiveSkill))
                continue;

            if (link.Supports.Any(s => SkillNameMatches(supportName, s)))
                return true;
        }

        return false;
    }

    public static bool IsForbiddenSupportOnActive(string activeSkillName, string supportName, MercSkillSet set)
    {
        if (string.IsNullOrWhiteSpace(activeSkillName) || string.IsNullOrWhiteSpace(supportName))
            return false;

        foreach (var tier in set.Tiers)
        {
            if (LinkListHasForbiddenSupport(tier.RequiredLinks, activeSkillName, supportName))
                return true;

            foreach (var loadout in tier.RequiredAnyLoadout)
            {
                if (LinkListHasForbiddenSupport(loadout.RequiredLinks, activeSkillName, supportName))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Set-level forbidden actives (always red). Tier-only forbids (e.g. Mirror Arrow on Better+)
    /// only block higher tiers; they are not highlighted as hard fails.
    /// </summary>
    public static bool IsForbiddenSkillName(string skillName, MercSkillSet set) =>
        set.ForbiddenSkills.Any(f => SkillNameMatches(skillName, f));

    private static bool LinkListHasForbiddenSupport(
        IReadOnlyList<LinkedSupportRequirement> links,
        string activeSkillName,
        string supportName)
    {
        foreach (var link in links)
        {
            if (!SkillNameMatches(activeSkillName, link.ActiveSkill))
                continue;

            if (link.ForbiddenSupports.Any(s => SkillNameMatches(supportName, s)))
                return true;
        }

        return false;
    }

    public static string GetLinkAnnotation(string skillName, IReadOnlyList<MercSkillSnapshot> skills, MercSkillSet set)
    {
        var notes = new List<string>();
        var links = set.Tiers
            .SelectMany(t => t.RequiredLinks.Concat(t.RequiredAnyLoadout.SelectMany(l => l.RequiredLinks)));

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
