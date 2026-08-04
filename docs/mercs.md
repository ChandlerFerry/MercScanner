# Merc scanner logic

What this plugin treats as a good merc, and how it decides.

## Data source

All skill and item checks run while the **mercenary encounter window** is open:

- **Items:** `MercenaryEncounterWindow.Inventories`
- **Skills + supports:** UI skill lines under the window (`2 → 10 → 0 → 1 → 0`), each child one skill row; supports under that row with names from cold tooltip text

## Match rules

Every set is **tier-only**. The rematch button highlights when any set matches (color = best tier rank).

| Rule | Meaning |
| --- | --- |
| **Tiers** | Ordered **best → worst** in JSON. First band that fully passes wins (rank 1 = best) |
| **Required on active** | That skill must be present; each listed support must be linked to it (empty supports = skill present only) |
| **Forbidden supports on active** | On that link’s `forbiddenSupports` — must not be linked to that active |
| **Need one of** | At least one option in the group on the relevant actives |
| **Either loadout** | On a tier: exactly one of `requiredAnyLoadout` packages must pass |
| **Forbidden (set)** | Must not appear as an active — blocks all tiers, red highlight |
| **Forbidden (tier)** | Blocks only that tier (e.g. Mirror Arrow on Better; Sellable still ok) |

### Colors (hardcoded)

| Rank | Meaning | Color |
| --- | --- | --- |
| 1 | Best | Dark green |
| 2 | Mid | Orange |
| 3 | Floor / third band | Blue |
| single-band | e.g. Sniper Full | Dark green (same as rank 1) |
| Valuable items | — | Dark green |

Active skill names highlight only when a tier matches. Supports still show partial blue for wishlist gems.

Source of truth: **`skill-sets.json`** (next to the plugin). Edit and reload the plugin.

---

## Manyshot (best → floor)

| Rank | Tier | Requirements |
| --- | --- | --- |
| 1 | **Better** | Ice + Vaal with GMP, Return, WED, Hypothermia; Vaal also CDR; Chain\|Fork; **no Mirror Arrow** |
| 2 | **Sellable** | Ice + Vaal each with **Return** (Mirror Arrow ok) |

**Forbidden (all tiers):** Icicle Rain

---

## Kineticist — Kinetic Blast of Clustering

| Rank | Tier | On KB of Clustering |
| --- | --- | --- |
| 1 | **Better** | GMP + WED + (Fork\|Chain) |
| 2 | **Sellable** | GMP + (Fork\|Chain) |

**Forbidden:** Barrage, Kinetic Rain of Impact, Flame Dash, Kinetic Bolt, Power Siphon

---

## Smoulderstrike

| Rank | Tier | Requirements |
| --- | --- | --- |
| 1 | **Better** | Both skills; Vaal GMP; MS GMP + WED + Gilded Eruption |
| 2 | **Sellable** | Both skills, each **GMP** |

**Forbidden:** Infernal Blow, Flamebolt Strike

---

## Sniper — Tornado Shot (single band)

Full package only (`Full`, dark green):

- GMP + Gilded Secondary Shots
- Need one of: Chain II / Greater Fork
- Forbidden supports on TS: Brutality, Arrow Nova

**Forbidden actives:** Shrapnel Ballista, Barrage of Volley Fire, Split Arrow, Puncture

---

## Combatant — Frost Blades + Static Strike + movement

Always required: **Static Strike**, **Frost Blades** with **Return + Chain** and **no Pierce**, and a movement skill (**Frostblink** or **Dash**).

Optional FB damage supports (max 5 total supports on FB; no Multistrike): WED, GMP, Hypothermia.

| Rank | Tier | Frost Blades supports | Movement |
| --- | --- | --- | --- |
| 1 | **Best** | Return + Chain + WED + GMP + Hypo (all 3) | Frostblink \| Dash |
| 2 | **Better** | Return + Chain + **1 of** WED \| GMP \| Hypo | Frostblink \| Dash |
| 3 | **Minimum** | Return + Chain | Frostblink \| Dash |

**Forbidden supports on FB:** Pierce, Greater Pierce  

**Forbidden actives:** Spectral Helix  
Wild Strike is allowed (no special highlight).

---

## Blade Ambusher

| Rank | Tier | Blade Trap | Spectral Helix of Trarthus | Spectral Throw of Trarthus |
| --- | --- | --- | --- | --- |
| 1 | **Better** | Multi Traps + Greater Throwing Speed | Multi Traps + Throw Speed + Slower Proj; **no** Faster Proj | Multiple Traps |
| 2 | **Sellable** | Multiple Traps | Multiple Traps | Present (any supports) |

**Forbidden actives:** Smoke Mine, Flame Dash

---

## Valuable items (any merc)

Alert when the encounter inventories show an item priced by **Ninja Price** at or above `AlertMinChaosValue` (default **10c**). Requires the Ninja Price plugin enabled. No name list — pricing only.
