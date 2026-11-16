# Skills++ (2026 fork)

Spend skill points on your skills as you level up. Every survivor, every DLC, survivors from other mods, and multiplayer that actually syncs.

Fork of [Skills++](https://thunderstore.io/package/CWMlolzlz/SkillsPlusPlus/) by CWMlolzlz, continued by Volvary and [icebro](https://thunderstore.io/package/icebro/SkillsPlusPlus/). Updated for **Risk of Rain 2 1.5.0**.

**Everyone in the lobby needs this mod.**

## How it works

- You get a skill point every 5 levels (configurable).
- Hold the **Buy Skills** key (bind it in Controls) or the scoreboard key, then click a skill or press its key to spend a point.
- Hover a skill to see what each level gives and your current level.
- Upgrades last for the run.

## What's new in this fork

- **Real multiplayer sync.** The host owns skill points and levels and sends them to everyone; clients ask the host to buy. Previous builds never ran the UNet weaver, so clients only got points at stage start and their purchases were local.
- **Per-player upgrades.** Effects calculated on the host use the acting player's own level. Several used to apply only to player 1 (Captain's DIABLO and Railgunner's Supercharge/Cryocharge cooldowns, among others).
- **Every skill can be upgraded.** All vanilla and DLC survivors have dedicated upgrades, including Seeker, False Son, Chef, Drifter, Operator, Acolyte and Enforcer. Survivors from other mods get dedicated upgrades where supported (Starstorm 2, Sandswept, Seamstress, EggsSkills' own integration) and generic upgrades otherwise.
- **Respec.** Press F8 (configurable) or type `spp_respec` in the console. By default only in the Bazaar Between Time, for 1 lunar coin.
- **Optional bonus points** for charged teleporters and every few stages.
- **Calmer Director (optional, on by default).** Caps how many monsters are alive at once (25 instead of 40) and makes elites rarer, without changing gold or XP. Turn it off in the config.

## Settings

Everything is in the config file and in Risk of Options: levels per point, linear vs compounding scaling, generic upgrade values, respec rules, bonus points, Calmer Director. In multiplayer the host's settings are used.

## For modders

The public API from the original mod still works (`SkillLevelModifier`, `SimpleSkillModifier<T>`, `SkillModifierManager.LoadSkillModifiers()`), so existing integrations such as EggsSkills keep working. New for modifiers:

- `SlotDamageBonusPerLevel(SkillSlot)`: host-side damage bonus per level for your skill's slot.
- `OnSkillEnterLate(state, level)`: runs after the state's own `OnEnter`.
- `UseGenericCooldownAndStock`: also give your skill the generic cooldown and charge bonuses.
- `SkillUpgrade.GetLevel(body, skillNames)`: a player's synced level, for host-side hooks.

Skills are matched by SkillDef asset name (or `skillName` when the asset has no name), case-insensitively.

## Credits

- CWMlolzlz: original Skills++.
- Volvary, icebro and contributors: earlier updates.
- This fork was written with the help of an AI coding assistant (Claude).

## Upgrades per level

Every skill can be upgraded. Skills marked *generic* get +15-20% damage (by slot). On top of that, any upgraded skill from another mod and every generic skill gets -6% cooldown if it has a cooldown, and +1 charge every 3 levels if it already has several charges.

### Commando

| Slot | Skill | Per level |
|---|---|---|
| Primary | Double Tap | +20% rate of fire and +15% recoil reduction |
| Primary | Flechette Rounds | dedicated upgrade from EggsSkills |
| Secondary | Phase Round | +30% damage and +30% projectile velocity |
| Secondary | Phase Blast | +30% bullets and +20% range |
| Utility | Tactical Dive | Grants +0.75 seconds of invulnerability |
| Utility | Tactical Slide | +0.125s slide duration. Converts +6% movespeed while sliding as attack speed, damage and armor |
| Utility | Tactical Pursuit | dedicated upgrade from EggsSkills |
| Special | Suppressive Fire | +30% bullets |
| Special | Frag Grenade | +20% damage and +20% blast size |
| Special | Sabotage | +20% damage, explosives last +3 s, +20% zap radius, +1 explosive every 2 levels |

### Nemesis Commando

| Slot | Skill | Per level |
|---|---|---|
| Primary | Blade of Cessation | +15% damage, swings 5% faster |
| Secondary | Single Tap | +20% damage, +20% range, +1 round every 2 levels |
| Secondary | Distant Gash | +20% damage, +15% beam speed |
| Utility | Tactical Roll | *generic* |
| Special | Submission | +20% damage, +1 bullet per volley every 2 levels |
| Special | Decisive Strike | +20% damage, charges 12% faster |

### Huntress

| Slot | Skill | Per level |
|---|---|---|
| Primary | Strafe | +20% range and +20% proc chance |
| Primary | Flurry | +10% range and +1 arrow fired |
| Secondary | Laser Glaive | +1 bounce, +10% damage, and +10 units of bounce range |
| Secondary | Laser Cyclone | +20% damage, +25% catch radius, +10% cooldown refunded on catch, +1 combo cap every 2 levels |
| Secondary | Explosive Arrow | dedicated upgrade from EggsSkills |
| Utility | Blink | Grants +1 second of crit time |
| Utility | Phase Blink | Grants +0.5 seconds of crit time |
| Special | Arrow Rain | +25% size and +20% damage |
| Special | Ballista | +1 bullet and +20% damage |

### Bandit

| Slot | Skill | Per level |
|---|---|---|
| Primary | Burst | +1 bullet, +5% damage |
| Primary | Blast | +20% damage, +10% proc chance, +20% stability |
| Primary | Bounce | dedicated upgrade from EggsSkills |
| Secondary | Serrated Dagger | +20% hitbox size, +20% damage |
| Secondary | Serrated Shiv | +1 shiv every two levels, +10% damage |
| Utility | Smoke Bomb | +15% hitbox size, +20% damage, +1 base movement speed while invisible |
| Utility | Kinetic Refractor | dedicated upgrade from EggsSkills |
| Special | Lights Out | +10% to +30% damage based on remaining enemy health |
| Special | Desperado | +1% Execute threshold |

### MUL-T

| Slot | Skill | Per level |
|---|---|---|
| Primary | Auto-Nailgun | +20% damage and +4 bullets for final burst |
| Primary | Rebar Puncher | +15% rate of fire and +10% damage |
| Primary | Scrap Launcher | +1 stock, +20% damage, and +15% blast size |
| Primary | Power-Saw | +20% rate of fire, +20% damage, and +30% hitbox size |
| Secondary | Blast Canister | +3 child bombs, +20% spread radius, +20% blast radius, and +20% damage |
| Secondary | Nanobot Swarm | dedicated upgrade from EggsSkills |
| Utility | Transport Mode | +10% duration, +10% speed, and +30% damage |
| Special | Retool | Grants +1 second of bonus attack speed and +1 second equipment cooldown |
| Special | Power Mode | Gain +25 armor, +15% damage and +10% speed |

### Engineer

| Slot | Skill | Per level |
|---|---|---|
| Primary | Bouncing Grenades | +1 and +2 to the minimum and maximum grenades fired |
| Primary | Laser Cutter | +15% damage, +25% turret and drone healing, +10% move speed while cutting, power stages 15% faster |
| Primary | Guided Salvo | dedicated upgrade from EggsSkills |
| Primary | Laser Focus | *generic* |
| Secondary | Pressure Mines | -10% arming time, +20% damaFge, +20% blast size, and +20% trigger range |
| Secondary | Spider Mines | +30% trigger range, +20% damage, +25% blast radius and +1 mine every 2 levels |
| Secondary | T-3514 Shock Mines | dedicated upgrade from EggsSkills |
| Utility | Bubble Shield | +15% duration and +15% size |
| Utility | Thermal Harpoons | +1 ammo, +20% damage, and +30% range |
| Utility | Rapid Displacement | *generic* |
| Special | TR12 Gauss Auto-Turret | +25% range and +10% damage. +1 turret every two levels |
| Special | TR58 Carbonizer Turret | +15% damage, +25% proc chance, and +10% rate of fire. +1 turret every two levels |

### Artificer

| Slot | Skill | Per level |
|---|---|---|
| Primary | Flame Bolt | +2 stock and -10% stock recharge time |
| Primary | Plasma Bolt | +2 stock and -10% stock recharge time |
| Secondary | Charged Nano-Bomb | +15% damage, +5% max charge time, and +10% blast size |
| Secondary | Cast Nano-Spear | +20% damage |
| Utility | Snapfreeze | +30% wall damage and +20% wall length |
| Utility | Tectonic Blast | +15% damage, +2 shards, -12% spread, burning oiled enemies take +15% damage |
| Utility | Quantum Transposition | dedicated upgrade from EggsSkills |
| Utility | Seismic Spear | *generic* |
| Special | Flamethrower | +20% range and +20% damage |
| Special | Ion Surge | +25% damage and +30% area of effect |

### Mercenary

| Slot | Skill | Per level |
|---|---|---|
| Primary | Laser Sword | +20% damage, and +15% attack speed |
| Secondary | Whirlwind | +25% damage and +25% larger hitbox |
| Secondary | Rising Thunder | +25% damage per level. +1 stock every two levels |
| Utility | Blinding Assault | +20% damage, and +0.5 seconds extra delay before reset |
| Utility | Focused Assault | +20% larger hitbox and +15% proc chance |
| Special | Eviscerate | +30% chain range, +15% attack speed, and +10% proc chance |
| Special | Slicing Winds | +20% attack speed and +20% damage |
| Special | Execute | dedicated upgrade from EggsSkills |

### Seamstress

| Slot | Skill | Per level |
|---|---|---|
| Primary | Trim | +15% damage, swings 5% faster, hits have a 5% chance to sew a Needle |
| Primary | Flurry | +15% damage, swings 6% faster, hits have a 5% chance to sew a Needle |
| Secondary | Clip | +20% damage, +1 snip every 2 levels |
| Secondary | Planar Manipulation | +20% damage, +15% grab range |
| Utility | Brutalize | +20% damage, +10% dash power, Insatiable lasts +0.5 s |
| Utility | Retaliate | +20% damage, parry window +0.08 s |
| Special | Skewer | +20% damage, throws 8% faster |

### Nemesis Mercenary

| Slot | Skill | Per level |
|---|---|---|
| Primary | Splatter | +15% damage, +1 pellet |
| Secondary | Cutthroat | +20% damage, +12% lunge distance |
| Secondary | Perforator | +20% damage, +25% auto-aim radius |
| Utility | Dispatch | +20% damage, attack speed burst for +0.5 s after blinking |
| Utility | Devitalize | +20% damage, +12% slide duration |
| Special | Exploit | +20% damage, +1 target every 2 levels |
| Special | Proliferate | +20% damage, clone lasts +1.5 s |

### REX

| Slot | Skill | Per level |
|---|---|---|
| Primary | DIRECTIVE: Inject | +1 syringe and +10% damage |
| Secondary | DIRECTIVE: Drill | +10% damage, +10% duration, and +20% radius |
| Secondary | Seed Barrage | +20% radius and +20% damage |
| Utility | DIRECTIVE: Disperse | +30% range, +20% angle, +20% debuff duration |
| Utility | Bramble Volley | +20% range, +20% angle, +20% damage |
| Utility | Proliferation | +20% damage, -3% health cost (min 8%), +5% heal on teleport |
| Special | DIRECTIVE: Harvest | +20% damage, +10% radius, +1% life steal |
| Special | Tangling Growth | +20% damage, +20% radius, +10% healing rate |
| Special | DIRECTIVE: Respire | dedicated upgrade from EggsSkills |

### Loader

| Slot | Skill | Per level |
|---|---|---|
| Primary | Knuckleboom | +1% barrier gained and +20% damage |
| Secondary | Grapple Fist | +30% hook range |
| Secondary | Spiked Fist | +15% hook range and +20% damage |
| Utility | Charged Gauntlet | +10% max charge, +20% base damage, and +15% velocity based damage |
| Utility | Thunder Gauntlet | +15% damage, +15% charge speed and +20% cone range |
| Special | M551 Pylon | +20% damage, +20% range, and +20% rate of fire |
| Special | Thunderslam | +15% damage, +10% to +100% range based on travel distance, +25% leap height |
| Special | Barrier Buster | dedicated upgrade from EggsSkills |

### Acrid

| Slot | Skill | Per level |
|---|---|---|
| Primary | Vicious Wounds | +20% normal damage, +25% combo finisher damage, and +15% attack speed |
| Primary | Caustic Spray | dedicated upgrade from EggsSkills |
| Secondary | Neurotoxin | +20% damage, +30% projectile speed, and +30% blast radius per level. +1 stock |
| Secondary | Ravenous Bite | +20% damage and +1 stock every 2 levels |
| Utility | Caustic Leap | +20% blast damage, +30% acid pool damage, and +20% acid pool size |
| Utility | Frenzied Leap | +25% blast damage and +15% refunded time |
| Special | Epidemic | +5 infection bounces, +20% infection range, and +20% on hit damage |
| Special | Cloying Breath | +20% damage, cloud lasts +1.5 s and is 15% bigger, slows enemies inside |
| Special | Expunge | dedicated upgrade from EggsSkills |

### Captain

| Slot | Skill | Per level |
|---|---|---|
| Primary | Vulcan Shotgun | +20% shell count and +10% damage |
| Primary | Mercury Repeater | dedicated upgrade from EggsSkills |
| Secondary | Power Tazer | +40% blast radius and +20% damage |
| Secondary | MK-4 Tracking Grenade | dedicated upgrade from EggsSkills |
| Utility | Orbital Probe | +20% blast radius and +20% damage |
| Utility | OGM-72 'DIABLO' Strike | -1 second drop time, -15% cooldown duration and +4% friendly fire prevention |
| Special | Orbital Supply Beacon | dedicated upgrade |

### Executioner

| Slot | Skill | Per level |
|---|---|---|
| Primary | Service Pistol | +15% damage, +10% bullet size |
| Primary | Deadly Voltage | +15% damage, +10% range, +1 chain every 2 levels |
| Secondary | Ion Manipulators | +20% damage, +1 maximum Ion Charge every 2 levels |
| Secondary | Consecration | +0.2 s cooldown reduction per charge, +1% healing |
| Utility | Crowd Dispersion | +20% damage, fear lasts +0.5 s, +10% radius |
| Special | Execution | +20% damage, +12% slam radius |

### Chirr

| Slot | Skill | Per level |
|---|---|---|
| Primary | Razor Leaves | +15% damage, +1 leaf |
| Secondary | Soothing Venom | *generic* |
| Utility | Abduct | +20% damage, +15% grab radius |
| Special | Symbiotic Link | *generic* |

### Railgunner

| Slot | Skill | Per level |
|---|---|---|
| Primary | XQR Smart Round System | +10% rate of fire and +20% range |
| Primary | G2 Lancer Rounds | dedicated upgrade from EggsSkills |
| Secondary | M99 Sniper | +1 max empowered round and +1 stock every two levels |
| Secondary | HH44 Marksman | +1 temporary armor per kill. Turns into +0.25s movement speed boost after zoom out |
| Utility | Concussion Device | +0.5s slowfall on hit and +15% throw range |
| Utility | Polar Field Device | +10% global damage on affected enemies, +15% range, and +15% throw range |
| Special | Supercharge | +10% proc chance and +20% crit multiplier |
| Special | Cryocharge | +2s frostfire burn and -10% cooldown duration |

### Void Fiend

| Slot | Skill | Per level |
|---|---|---|
| Primary | Drown | +1s of slow on hit and +10% damage   [Corrupted]  +25% range, +10% damage and +15% extra speed while in use |
| Secondary | Flood | +15% faster charge speed, +10% damage and 15% bigger projectile   [Corrupted] +15% damage and +10% less cooldown |
| Utility | Trespass | +50 armor for 3 seconds  [Corrupted] 20% attack speed and 20% speed for +1 second |
| Special | Supress | +15% extra health on use  [Corrupted] +15% extra corruption on use |
| Special | Consume | dedicated upgrade from EggsSkills |

### Seeker

| Slot | Skill | Per level |
|---|---|---|
| Primary | Spirit Punch | +15% damage, +3% damage per Tranquility stack |
| Secondary | Unseen Hand | +20% damage, +20% range, heals +1.5% of damage dealt |
| Secondary | Soul Spiral | +15% damage, +1 maximum orb (up to +4) |
| Utility | Sojourn | +20% damage, -1% health cost per tick (min 3.5%), +12% explosion size |
| Utility | Reprieve | +15% damage, +8% barrier, +3% barrier for allies |
| Special | Meditate | +20% damage, +1 s to enter the sequence (up to +4 s), smaller penalties. Level 3+: first wrong arrow is forgiven |
| Special | Palm Blast | +20% damage, charges 15% faster, 15% bigger hand. Level 2+: Tranquility after hitting 2 characters |

### False Son

| Slot | Skill | Per level |
|---|---|---|
| Primary | Club of the Forsaken | +15% damage, +20% slam radius |
| Secondary | Lunar Spikes | +15% damage, +1 spike, spikes regenerate on their own (faster each level, at most every 2 s) |
| Secondary | Lunar Stakes | +20% damage, lightning arcs to +1 enemy, +1 stake every 2 levels |
| Utility | Step of the Brothers | +15% damage, +20% blast radius, +1 use every 2 levels |
| Utility | Meridian's Will | +15% damage, passive tripled for +0.75 s, +20% pull radius |
| Special | Laser of the Father | +20% damage, +0.5 s duration, charges 15% faster |
| Special | Laser Burst | +20% damage, refills +12% more spikes |

### CHEF

| Slot | Skill | Per level |
|---|---|---|
| Primary | Dice | +10m extra cleaver distance, +25% extra damage and +20% returning cleaver size  [Boosted] +4 extra cleavers |
| Secondary | Sear | +0.5% health on hit and +25% attack speed   [Boosted] +1 extra fireball |
| Secondary | Ice Box | dedicated upgrade |
| Utility | Roll | dedicated upgrade |
| Utility | Oil Spill | +2 oil jumps, gasoline explosions deal +30% damage with +15% radius  [Boosted] +2 boosted oil jumps |
| Special | Glaze | +3 extra oil globs |
| Special | Yes, CHEF! | Temporarily increase skill points of the next used skill by +1. |

### Operator

| Slot | Skill | Per level |
|---|---|---|
| Primary | H3-11 OCR Custom | +15% damage, charges build 8% faster, from level 2 fully charged shots pierce enemies |
| Secondary | ADMIN-OVERRIDE | -10% cooldown, +1 charge every 2 levels |
| Secondary | CMD-SWARM | drone rams: +20% damage, +15% radius, +0.3 s shock |
| Utility | Ascent Protocol | +15% damage, +10% leap speed, landing sends a shockwave for 200% damage |
| Utility | FIREWALL | +20% damage, +20% shield duration |
| Special | Ejection Core | +20% damage, +15% knock-up radius, launched enemies slam the ground on landing for 100% damage |
| Special | Amp Core | +20% damage, stores +15% more damage, +25% damage cap, +10% max blast radius |

### Drifter

| Slot | Skill | Per level |
|---|---|---|
| Primary | Blunt Force | +20% damage |
| Secondary | Cleanup | +15% damage, +1 debris |
| Secondary | Junk Cube | +20% damage, +5% junk drop chance |
| Utility | Repossess | +20% damage, monsters take 25% longer to break free |
| Utility | Tornado Slam | +20% damage, +25% extra spin time per bounce |
| Special | Salvage | +1 temporary item |
| Special | Tinker | +25% projectile size and debuff duration. Gain half a temporary item when tinkering with items every two levels. |

### Enforcer

| Slot | Skill | Per level |
|---|---|---|
| Primary | Trenchbroom | +15% damage, damage falloff starts 5 m later |
| Primary | Shrapnel Grenade | +20% damage |
| Secondary | Steel Protection | +20% damage, +20% deflect damage, -10% stance slow, first parry refunds 0.5 s utility cooldown |
| Secondary | Surge Protection | +20% damage, +1 s duration, absorbs +12% max health, +15% explosion radius |
| Utility | Shield Tackle | +20% damage, +12% charge speed |
| Utility | Ripcord | +20% damage |
| Special | Rally Point | +20% damage, +20% slam radius |
| Special | Crash Point | +25% damage, +15% impact radius, impact slows nearby enemies |

### Ranger

| Slot | Skill | Per level |
|---|---|---|
| Primary | Direct Current | +15% damage, fires 5% faster |
| Secondary | Release | +20% damage, +10% damage at full Charge |
| Secondary | Galvanize | +20% damage, +1 maximum orb |
| Utility | Sidestep | +10% duration |
| Special | Overdrive | *generic* |

### VOL-T

| Slot | Skill | Per level |
|---|---|---|
| Primary | Galvanic Bolt | +15% damage, +15% zap radius and range |
| Secondary | Tempest Sphere | +20% damage, +10% tick rate |
| Utility | Static Snare | +20% damage, +15% explosion radius, +20% damage on the zip |
| Special | Signal Overload | +20% damage, +0.4 s duration, +10% radius |

### Acolyte

| Slot | Skill | Per level |
|---|---|---|
| Primary | Blade Slice | +15% damage, +1 quickstrike every 2 levels |
| Secondary | Bola Throw | +20% damage, gathers +1 enemy |
| Secondary | Soul Jar | +20% damage, +1 wight from each haunted kill |
| Utility | Blade Sling | +20% damage, Blade Dash grants +0.3 s invulnerability |
| Utility | Javelin | +20% damage, +20% explosion radius |
| Special | Sacrifice | +20% damage, +1 Conviction every 2 levels (up to +2) |

