# Merc scanner logic

What this plugin treats as a good merc, and how it decides.

## Match rules

Each named set is a wishlist. On idle world mercs, the overlay shows skills and a **MATCH** banner when a set fully passes.

| Rule | Meaning |
| --- | --- |
| **Required on active** | That skill must be present, and each listed support must be linked to it |
| **Need one of** | At least one option in the group must be present |
| **Either loadout** | Exactly one of the alternative packages must fully pass (Combatant) |
| **Forbidden** | Must not appear on the merc at all |

**Both** means every listed active package is required.  
**Either** means only one package needs to pass.

Supports are checked per active skill when the game exposes useful stats (e.g. GMP via additional projectiles). Otherwise presence of the support name on the merc is used. Overlay tags look like `Molten Strike [+GMP3 +WED3]`.

Rucksack/gear valuables are a separate path: any merc type, only while the hire/encounter window is open.

Source of truth for the lists: `MercProfiles.cs`.

---

## Manyshot — both Ice Shot and Vaal Ice Shot

### Ice Shot

- **Required:** GMP III, Return III, WED III, Greater Hypothermia III
- **Need one of:** Chain II, Greater Fork III

### Vaal Ice Shot

- **Required:** GMP III, Return III, WED III, Greater Hypothermia III, Greater Cooldown Recovery III
- **Need one of:** Chain II, Greater Fork III (same group as above)

### Forbidden

- Icicle Rain
- Mirror Arrow

---

## Kineticist — Kinetic Blast of Clustering

- **Required:** GMP III (on KB of Clustering), WED III
- **Need one of:** Greater Fork III, Chain II

### Forbidden

- Barrage
- Kinetic Rain of Impact
- Flame Dash
- Kinetic Bolt
- Power Siphon

---

## Smoulderstrike — both Vaal Molten Strike and Molten Strike

| Active | Required supports |
| --- | --- |
| Vaal Molten Strike | GMP III only |
| Molten Strike | GMP III, WED III, Gilded Molten Eruption III |

### Forbidden

- Infernal Blow (any variant)
- Flamebolt Strike

---

## Sniper — Tornado Shot

- **Required:** GMP III, Gilded Secondary Shots III
- **Need one of:** Chain II, Greater Fork III

### Forbidden

- Shrapnel Ballista
- Barrage of Volley Fire
- Split Arrow (not Greater Split Arrow)
- Puncture
- Arrow Nova III
- Brutality (any tier)

---

## Combatant — either Frost Blades or Static Strike

### Frost Blades

- **Required:** Return III, WED III, GMP III, Greater Multistrike III, Greater Hypothermia III
- **Need one of:** Chain II, Greater Pierce III

### Static Strike

- **Required:** Greater More Duration III, WED III, Chain II

### Forbidden (either path)

- Wild Strike
- Spectral Helix

Banner names the path: `MATCH: Combatant (Frost Blades)` or `MATCH: Combatant (Static Strike)`.

---

## Blade Ambusher — both Blade Trap and Spectral Helix of Trarthus

| Active | Required supports |
| --- | --- |
| Blade Trap | Multiple Traps III, Greater Throwing Speed III |
| Spectral Helix of Trarthus | Multiple Traps III, Greater Throwing Speed III |

### Forbidden

- Spectral Throw of Trarthus
- Smoke Mine
- Flame Dash
- Faster Projectiles (any tier)

---

## Valuable items (any merc)

Alert when the encounter inventories show any of:

- Divine Orb, Chaos Orb, Eternal Orb
- Mirror of Kalandra, Hinekora's Lock
- Headhunter, Mageblood, Progenesis, Original Sin
- Rakiata's Dance, Defiance of Destiny, Astramentis
- Horned Scarab of Glittering
