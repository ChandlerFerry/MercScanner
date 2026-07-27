using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore.Shared.Enums;

namespace MercScanner;

/// <summary>
/// Snapshot of one ActorSkill on a merc (name + stats for link checks).
/// </summary>
public sealed class MercSkillSnapshot
{
    public MercSkillSnapshot(string name, IReadOnlyDictionary<GameStat, int> stats)
    {
        Name = name ?? "";
        Stats = stats ?? new Dictionary<GameStat, int>();
    }

    public string Name { get; }
    public IReadOnlyDictionary<GameStat, int> Stats { get; }

    public int GetStat(GameStat stat) =>
        Stats.TryGetValue(stat, out var value) ? value : 0;
}

/// <summary>
/// Active skill that must have specific supports linked (verified via skill stats when possible).
/// </summary>
public sealed class LinkedSupportRequirement
{
    public LinkedSupportRequirement(string activeSkill, params string[] supports)
    {
        ActiveSkill = activeSkill;
        Supports = supports ?? Array.Empty<string>();
    }

    public string ActiveSkill { get; }
    public IReadOnlyList<string> Supports { get; }
}

/// <summary>
/// One complete alternative loadout (e.g. Frost Blades package OR Static Strike package).
/// All of its links / any-of groups must pass for this loadout to match.
/// </summary>
public sealed class MercLoadout
{
    public MercLoadout(
        string name,
        IReadOnlyList<LinkedSupportRequirement> requiredLinks = null,
        IReadOnlyList<IReadOnlyList<string>> requiredAnyOfGroups = null)
    {
        Name = name;
        RequiredLinks = requiredLinks ?? Array.Empty<LinkedSupportRequirement>();
        RequiredAnyOfGroups = requiredAnyOfGroups ?? Array.Empty<IReadOnlyList<string>>();
    }

    public string Name { get; }
    public IReadOnlyList<LinkedSupportRequirement> RequiredLinks { get; }
    public IReadOnlyList<IReadOnlyList<string>> RequiredAnyOfGroups { get; }
}

/// <summary>
/// A hardcoded wishlist for a mercenary archetype.
/// A merc "matches" when it has every required skill, satisfies every any-of group,
/// satisfies every linked-support requirement (or at least one alternative loadout),
/// and has none of the forbidden skills.
/// </summary>
public sealed class MercSkillSet
{
    public MercSkillSet(
        string name,
        IReadOnlyList<string> requiredSkills = null,
        IReadOnlyList<string> forbiddenSkills = null,
        IReadOnlyList<IReadOnlyList<string>> requiredAnyOfGroups = null,
        IReadOnlyList<LinkedSupportRequirement> requiredLinks = null,
        IReadOnlyList<MercLoadout> requiredAnyLoadout = null,
        IReadOnlyList<string> typeMatchers = null)
    {
        Name = name;
        RequiredSkills = requiredSkills ?? Array.Empty<string>();
        ForbiddenSkills = forbiddenSkills ?? Array.Empty<string>();
        RequiredAnyOfGroups = requiredAnyOfGroups ?? Array.Empty<IReadOnlyList<string>>();
        RequiredLinks = requiredLinks ?? Array.Empty<LinkedSupportRequirement>();
        RequiredAnyLoadout = requiredAnyLoadout ?? Array.Empty<MercLoadout>();
        TypeMatchers = typeMatchers ?? Array.Empty<string>();
    }

    /// <summary>Display name, e.g. "Manyshot".</summary>
    public string Name { get; }

    /// <summary>
    /// Optional identity filters (matched against Metadata / Path / RenderName).
    /// When empty, the set is evaluated against every merc (skill-only filter).
    /// </summary>
    public IReadOnlyList<string> TypeMatchers { get; }

    /// <summary>Skills that must all be present (name match).</summary>
    public IReadOnlyList<string> RequiredSkills { get; }

    /// <summary>
    /// Each inner list is an OR-group: at least one skill from the group must be present.
    /// All groups must be satisfied (AND of ORs).
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>> RequiredAnyOfGroups { get; }

    /// <summary>
    /// Active skills that must have the given supports linked.
    /// GMP III is verified via NumberOfAdditionalProjectiles on that active skill.
    /// </summary>
    public IReadOnlyList<LinkedSupportRequirement> RequiredLinks { get; }

    /// <summary>
    /// Alternative full packages — at least one loadout must fully match.
    /// Used when a merc type has mutually exclusive good skill lines (e.g. Combatant).
    /// </summary>
    public IReadOnlyList<MercLoadout> RequiredAnyLoadout { get; }

    /// <summary>Skills that must all be absent (name match).</summary>
    public IReadOnlyList<string> ForbiddenSkills { get; }
}

public static class MercProfiles
{
    /// <summary>
    /// Edit this list to define which merc skill loadouts you care about.
    /// </summary>
    public static readonly IReadOnlyList<MercSkillSet> SkillSets =
    [
        // Manyshot:
        // Ice Shot + Vaal Ice Shot both want GMP3, Return III, WED3, Greater Hypothermia III,
        // and prefer Chain II or Greater Fork III. Vaal Ice Shot also wants Greater Cooldown Recovery III.
        // Ban Icicle Rain / Mirror Arrow.
        new MercSkillSet(
            name: "Manyshot",
            requiredLinks:
            [
                new LinkedSupportRequirement(
                    "Ice Shot",
                    "Greater Multiple Projectiles III",
                    "Return III",
                    "Greater Elemental Damage with Attacks III",
                    "Greater Hypothermia III"),
                new LinkedSupportRequirement(
                    "Vaal Ice Shot",
                    "Greater Multiple Projectiles III",
                    "Return III",
                    "Greater Elemental Damage with Attacks III",
                    "Greater Hypothermia III",
                    "Greater Cooldown Recovery III"),
            ],
            requiredAnyOfGroups:
            [
                // (1) preferred — need at least one
                ["Chain II", "Greater Fork III"],
            ],
            forbiddenSkills:
            [
                "Icicle Rain",
                "Mirror Arrow",
            ]),

        // Kineticist: KB of Clustering with GMP III + (Fork III or Chain II) + Greater Ele Dmg III.
        new MercSkillSet(
            name: "Kineticist",
            requiredSkills:
            [
                "Kinetic Blast of Clustering",
                "Greater Elemental Damage with Attacks III",
            ],
            requiredLinks:
            [
                new LinkedSupportRequirement(
                    "Kinetic Blast of Clustering",
                    "Greater Multiple Projectiles III"),
            ],
            requiredAnyOfGroups:
            [
                // Need Greater Fork III OR Chain II (name presence; may also be on skill stats)
                ["Greater Fork III", "Chain II"],
            ],
            forbiddenSkills:
            [
                "Barrage",
                "Kinetic Rain of Impact",
                "Flame Dash",
                "Kinetic Bolt",
                "Power Siphon",
            ]),

        // Smoulderstrike:
        // - VMS: GMP III only
        // - MS: GMP III + WED III + Gilded Molten Eruption III
        // Ban Infernal Blow / Flamebolt Strike.
        new MercSkillSet(
            name: "Smoulderstrike",
            requiredLinks:
            [
                new LinkedSupportRequirement(
                    "Vaal Molten Strike",
                    "Greater Multiple Projectiles III"),
                new LinkedSupportRequirement(
                    "Molten Strike",
                    "Greater Multiple Projectiles III",
                    "Greater Elemental Damage with Attacks III",
                    "Gilded Molten Eruption III"),
            ],
            forbiddenSkills:
            [
                "Infernal Blow",
                "Flamebolt Strike",
            ]),

        // Sniper: Tornado Shot with GMP3 + Gilded Secondary Shots III.
        // Prefer Chain II or Greater Fork III (at least one). Ban Arrow Nova / Brutality / bad secondaries.
        new MercSkillSet(
            name: "Sniper",
            requiredLinks:
            [
                new LinkedSupportRequirement(
                    "Tornado Shot",
                    "Greater Multiple Projectiles III",
                    "Gilded Secondary Shots III"),
            ],
            requiredAnyOfGroups:
            [
                // (1) preferred projectile supports — need at least one
                ["Chain II", "Greater Fork III"],
            ],
            forbiddenSkills:
            [
                "Shrapnel Ballista",
                "Barrage of Volley Fire",
                "Split Arrow", // not Greater Split Arrow (name match is exact-prefix aware)
                "Puncture",
                "Arrow Nova III",
                "Brutality", // Lesser/Brutality/Greater Brutality I-III
            ]),

        // Combatant: full Frost Blades package OR full Static Strike package.
        // Must not have Wild Strike or Spectral Helix either way.
        new MercSkillSet(
            name: "Combatant",
            requiredAnyLoadout:
            [
                new MercLoadout(
                    name: "Frost Blades",
                    requiredLinks:
                    [
                        new LinkedSupportRequirement(
                            "Frost Blades",
                            "Return III",
                            "Greater Elemental Damage with Attacks III",
                            "Greater Multiple Projectiles III",
                            "Greater Multistrike III",
                            "Greater Hypothermia III"),
                    ],
                    requiredAnyOfGroups:
                    [
                        // (1) preferred projectile supports
                        ["Chain II", "Greater Pierce III"],
                    ]),
                new MercLoadout(
                    name: "Static Strike",
                    requiredLinks:
                    [
                        new LinkedSupportRequirement(
                            "Static Strike",
                            "Greater More Duration III",
                            "Greater Elemental Damage with Attacks III",
                            "Chain II"),
                    ]),
            ],
            forbiddenSkills:
            [
                "Wild Strike",
                "Spectral Helix",
            ]),

        // Blade Ambusher: Blade Trap + Spectral Helix of Trarthus, both with Multi Trap 3 + Greater Throwing Speed 3.
        // No Spectral Throw of Trarthus / Smoke Mine / Flame Dash / Faster Projectiles.
        new MercSkillSet(
            name: "Blade Ambusher",
            requiredLinks:
            [
                new LinkedSupportRequirement(
                    "Blade Trap",
                    "Multiple Traps III",
                    "Greater Throwing Speed III"),
                new LinkedSupportRequirement(
                    "Spectral Helix of Trarthus",
                    "Multiple Traps III",
                    "Greater Throwing Speed III"),
            ],
            forbiddenSkills:
            [
                "Spectral Throw of Trarthus",
                "Smoke Mine",
                "Flame Dash",
                "Faster Projectiles", // Lesser / II / Greater Faster Projectiles
            ]),
    ];

    /// <summary>
    /// Rucksack / merc inventory items that should always alert, on any merc type.
    /// Matched case-insensitively against Base.Name, RenderName, and Metadata.
    /// </summary>
    public static readonly IReadOnlyList<string> ValuableItems =
    [
        "Divine Orb",
        "Chaos Orb",
        "Mirror of Kalandra",
        "Hinekora's Lock",
        "Eternal Orb",
        "Headhunter",
        "Mageblood",
        "Progenesis",
        "Original Sin",
        "Rakiata's Dance",
        "Defiance of Destiny",
        "Astramentis",
        "Horned Scarab of Glittering",
    ];

    public static bool SkillNameMatches(string skillName, string pattern)
    {
        if (string.IsNullOrWhiteSpace(skillName) || string.IsNullOrWhiteSpace(pattern))
            return false;

        // Exact: "Molten Strike" == "Molten Strike"
        if (skillName.Equals(pattern, StringComparison.InvariantCultureIgnoreCase))
            return true;

        // Transfigured: "Infernal Blow" matches "Infernal Blow of Immolation"
        if (skillName.StartsWith(pattern + " of ", StringComparison.InvariantCultureIgnoreCase))
            return true;

        // Avoid "Split Arrow" matching "Greater Split Arrow", "Molten Strike" matching "Vaal Molten Strike"
        foreach (var prefix in new[] { "Greater ", "Lesser ", "Vaal ", "Awakened ", "Gilded " })
        {
            if (!skillName.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase) ||
                pattern.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase))
                continue;

            var rest = skillName[prefix.Length..];
            if (rest.Equals(pattern, StringComparison.InvariantCultureIgnoreCase))
                return false;
            if (rest.StartsWith(pattern + " of ", StringComparison.InvariantCultureIgnoreCase))
                return false;
        }

        // Supports / partial names: "Brutality" in "Greater Brutality III", full support names, etc.
        return skillName.Contains(pattern, StringComparison.InvariantCultureIgnoreCase);
    }

    public static bool HasSkill(IEnumerable<string> skillNames, string pattern)
    {
        return skillNames.Any(s => SkillNameMatches(s, pattern));
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

    /// <summary>
    /// Whether an active skill has a given support linked.
    /// Stat-backed when possible (GMP / chain / fork / secondary shots);
    /// otherwise falls back to support name appearing anywhere on the merc.
    /// </summary>
    public static bool ActiveSkillHasSupport(
        MercSkillSnapshot skill,
        string supportName,
        IReadOnlyList<MercSkillSnapshot> allSkills)
    {
        if (skill == null || string.IsNullOrWhiteSpace(supportName))
            return false;

        if (IsGreaterMultipleProjectilesIii(supportName))
        {
            // GMP III: Supported Skills fire 3 additional Projectiles
            return skill.GetStat(GameStat.NumberOfAdditionalProjectiles) >= 3;
        }

        if (IsReturnIii(supportName))
        {
            if (skill.GetStat(GameStat.ProjectilesReturn) > 0)
                return true;
            if (skill.GetStat(GameStat.AttackProjectilesReturn) > 0)
                return true;
            return HasSkill(allSkills.Select(s => s.Name), supportName);
        }

        if (IsGildedSecondaryShotsIii(supportName))
        {
            // Tornado Shot base is +3 secondary; Gilded Secondary Shots III adds +2 → 5.
            if (skill.GetStat(GameStat.TornadoShotNumOfSecondaryProjectiles) >= 5)
                return true;
            return HasSkill(allSkills.Select(s => s.Name), supportName);
        }

        if (IsChainIi(supportName))
        {
            // Chain II: Supported Skills Chain +3 times
            if (skill.GetStat(GameStat.NumberOfAdditionalChainsForProjectiles) >= 3)
                return true;
            if (skill.GetStat(GameStat.NumberOfChains) >= 3)
                return true;
            if (skill.GetStat(GameStat.VirtualNumberOfChains) >= 3)
                return true;
            return HasSkill(allSkills.Select(s => s.Name), supportName);
        }

        if (IsGreaterForkIii(supportName))
        {
            if (skill.GetStat(GameStat.ProjectilesFork) > 0)
                return true;
            if (skill.GetStat(GameStat.VirtualProjectilesFork) > 0)
                return true;
            if (skill.GetStat(GameStat.AttackProjectilesFork) > 0)
                return true;
            return HasSkill(allSkills.Select(s => s.Name), supportName);
        }

        if (IsGreaterMultistrikeIii(supportName))
        {
            // Greater Multistrike III: Supported Skills Repeat 2 additional times
            if (skill.GetStat(GameStat.BaseAttackRepeatCount) >= 2)
                return true;
            if (skill.GetStat(GameStat.AttackRepeatCount) >= 2)
                return true;
            if (skill.GetStat(GameStat.SkillRepeatCount) >= 2)
                return true;
            return HasSkill(allSkills.Select(s => s.Name), supportName);
        }

        if (IsGreaterPierceIii(supportName))
        {
            // Greater Pierce III: Pierce 5 additional Targets
            if (skill.GetStat(GameStat.ProjectileNumberOfTargetsToPierce) >= 5)
                return true;
            if (skill.GetStat(GameStat.ArrowNumberOfTargetsToPierce) >= 5)
                return true;
            return HasSkill(allSkills.Select(s => s.Name), supportName);
        }

        // WED, Hypothermia, Cooldown Recovery, More Duration, Gilded Molten Eruption, etc.:
        // no reliable per-skill stat — name presence fallback.
        return HasSkill(allSkills.Select(s => s.Name), supportName);
    }

    public static bool IsGreaterMultipleProjectilesIii(string supportName) =>
        supportName.Contains("Greater Multiple Projectiles III", StringComparison.InvariantCultureIgnoreCase)
        || supportName.Equals("GMP III", StringComparison.InvariantCultureIgnoreCase)
        || supportName.Equals("GMP3", StringComparison.InvariantCultureIgnoreCase);

    public static bool IsReturnIii(string supportName) =>
        supportName.Contains("Return III", StringComparison.InvariantCultureIgnoreCase)
        || supportName.Equals("Return 3", StringComparison.InvariantCultureIgnoreCase);

    public static bool IsGildedSecondaryShotsIii(string supportName) =>
        supportName.Contains("Gilded Secondary Shots III", StringComparison.InvariantCultureIgnoreCase);

    public static bool IsChainIi(string supportName) =>
        supportName.Equals("Chain II", StringComparison.InvariantCultureIgnoreCase)
        || supportName.Contains("Chain II", StringComparison.InvariantCultureIgnoreCase);

    public static bool IsGreaterForkIii(string supportName) =>
        supportName.Contains("Greater Fork III", StringComparison.InvariantCultureIgnoreCase);

    public static bool IsGreaterMultistrikeIii(string supportName) =>
        supportName.Contains("Greater Multistrike III", StringComparison.InvariantCultureIgnoreCase);

    public static bool IsGreaterPierceIii(string supportName) =>
        supportName.Contains("Greater Pierce III", StringComparison.InvariantCultureIgnoreCase);

    public static string ShortSupportName(string supportName)
    {
        if (IsGreaterMultipleProjectilesIii(supportName)) return "GMP3";
        if (IsReturnIii(supportName)) return "Return3";
        if (IsGildedSecondaryShotsIii(supportName)) return "GildSec3";
        if (supportName.Contains("Gilded Molten Eruption", StringComparison.InvariantCultureIgnoreCase))
            return "GildErupt3";
        if (supportName.Contains("Greater Elemental Damage with Attacks", StringComparison.InvariantCultureIgnoreCase))
            return "WED3";
        if (supportName.Contains("Greater Hypothermia", StringComparison.InvariantCultureIgnoreCase)
            || supportName.Contains("Hypothermia III", StringComparison.InvariantCultureIgnoreCase))
            return "Hypo3";
        if (supportName.Contains("Cooldown Recovery", StringComparison.InvariantCultureIgnoreCase))
            return "CDR3";
        if (supportName.Contains("Greater More Duration", StringComparison.InvariantCultureIgnoreCase)
            || supportName.Contains("More Duration III", StringComparison.InvariantCultureIgnoreCase))
            return "MoreDur3";
        if (IsGreaterMultistrikeIii(supportName)) return "Multi3";
        if (IsGreaterPierceIii(supportName)) return "Pierce3";
        if (IsChainIi(supportName)) return "Chain2";
        if (IsGreaterForkIii(supportName)) return "Fork3";
        if (supportName.Contains("Multiple Traps", StringComparison.InvariantCultureIgnoreCase))
            return "MultiTrap3";
        if (supportName.Contains("Throwing Speed", StringComparison.InvariantCultureIgnoreCase))
            return "ThrowSpd3";
        if (supportName.Contains("Arrow Nova", StringComparison.InvariantCultureIgnoreCase)) return "ArrowNova3";
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

        var skillNames = skills.Select(s => s.Name).ToList();

        foreach (var forbidden in set.ForbiddenSkills)
        {
            if (HasSkill(skillNames, forbidden))
                return false;
        }

        foreach (var required in set.RequiredSkills)
        {
            if (!HasSkill(skillNames, required))
                return false;
        }

        foreach (var group in set.RequiredAnyOfGroups)
        {
            if (group == null || group.Count == 0)
                continue;

            if (!group.Any(option => HasSkill(skillNames, option)))
                return false;
        }

        foreach (var link in set.RequiredLinks)
        {
            if (!LinkRequirementMet(skills, link))
                return false;
        }

        // Alternative packages: at least one full loadout must pass.
        if (set.RequiredAnyLoadout.Count > 0)
        {
            if (!set.RequiredAnyLoadout.Any(loadout => LoadoutMatches(skills, loadout)))
                return false;
        }

        return true;
    }

    /// <summary>Name of the first matching alternative loadout, or null.</summary>
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

        var skillNames = skills.Select(s => s.Name).ToList();

        foreach (var link in loadout.RequiredLinks)
        {
            if (!LinkRequirementMet(skills, link))
                return false;
        }

        foreach (var group in loadout.RequiredAnyOfGroups)
        {
            if (group == null || group.Count == 0)
                continue;

            if (!group.Any(option => HasSkill(skillNames, option)))
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

    /// <summary>
    /// Annotate an active skill with missing/present linked supports for overlay text.
    /// Returns null when no link requirements apply to this skill name.
    /// </summary>
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

    public static bool IsValuableItem(string baseName, string renderName, string metadata)
    {
        foreach (var valuable in ValuableItems)
        {
            if (string.IsNullOrWhiteSpace(valuable))
                continue;

            if (!string.IsNullOrEmpty(baseName) &&
                baseName.Contains(valuable, StringComparison.InvariantCultureIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(renderName) &&
                renderName.Contains(valuable, StringComparison.InvariantCultureIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(metadata) &&
                metadata.Contains(valuable.Replace(" ", ""), StringComparison.InvariantCultureIgnoreCase))
                return true;
        }

        return false;
    }
}
