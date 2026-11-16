using EntityStates;
using EntityStates.Commando.CommandoWeapon;
using EntityStates.Commando.StickyGrenade;
using EntityStates.Croco;
using EntityStates.Engi.EngiWeapon;
using EntityStates.Huntress.Cyclone;
using EntityStates.Huntress.HuntressWeapon;
using EntityStates.Mage.Weapon;
using EntityStates.Treebot;
using RoR2;
using RoR2.Projectile;
using SkillsPlusPlus.Generic;
using SkillsPlusPlus.Util;
using UnityEngine;

// Upgrades for vanilla alt skills added after the fork was last updated (game 1.4/1.5).
// Damage comes from the host-side slot bonus (SlotDamageBonusPerLevel); these classes add the mechanics.
namespace SkillsPlusPlus.Modifiers
{
    /// <summary>Commando – Sabotage: longer-lasting charges, bigger zaps, an extra charge every 2 levels.</summary>
    [SkillLevelModifier("CommandoSabotageSpread", typeof(SabotageSpread), typeof(Sabotage), typeof(StickyGrenadeStuckState), typeof(StickyGrenadeExplodeState))]
    internal class CommandoSabotageSkillModifier : BaseSkillModifier
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.10f;

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            switch (skillState)
            {
                case Sabotage sabotage:
                    sabotage.projectileCount += level / 2;
                    break;
                case StickyGrenadeStuckState _:
                    // Runs on the projectile, on the host, right before the despawn timer starts.
                    StickyGrenadeStuckState.timeBeforeDespawn = Originals.Get("Sticky.timeBeforeDespawn", StickyGrenadeStuckState.timeBeforeDespawn) + 3f * level;
                    break;
                case StickyGrenadeExplodeState _:
                    StickyGrenadeExplodeState.blastRadius = MultScaling(Originals.Get("Sticky.blastRadius", StickyGrenadeExplodeState.blastRadius), 0.15f, level);
                    break;
            }
        }
    }

    /// <summary>Huntress – Laser Cyclone: easier to catch, refunds more cooldown, higher combo cap.</summary>
    [SkillLevelModifier("ThrowCyclone_Kick", typeof(ThrowCycloneKickable), typeof(CatchCyclone), typeof(FlyOut), typeof(Return))]
    internal class HuntressCycloneSkillModifier : BaseSkillModifier
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.08f;

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            if (skillState is Return || skillState is FlyOut)
            {
                Return.catchRadius = MultScaling(Originals.Get("Cyclone.catchRadius", Return.catchRadius), 0.25f, level);
                Return.catchRechargeFraction = Mathf.Min(1f, Originals.Get("Cyclone.catchRecharge", Return.catchRechargeFraction) + 0.10f * level);
                BaseCycloneState.MaxComboValue = Originals.Get("Cyclone.maxCombo", BaseCycloneState.MaxComboValue) + level / 2;
            }
        }
    }

    /// <summary>Artificer – Tectonic Blast: more shards, tighter spread. Burning oiled enemies hurts more.</summary>
    [SkillLevelModifier("MageBodyFireEarthShards", typeof(FireEarthShards))]
    internal class MageTectonicBlastSkillModifier : SimpleSkillModifier<FireEarthShards>
    {
        private const string SkillName = "MageBodyFireEarthShards";

        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.08f;

        public override void OnSkillEnter(FireEarthShards skillState, int level)
        {
            FireEarthShards.maxProjectiles = Originals.Get("EarthShards.maxProjectiles", FireEarthShards.maxProjectiles) + 2 * level;
            FireEarthShards.maxSpread = MultScaling(Originals.Get("EarthShards.maxSpread", FireEarthShards.maxSpread), -0.12f, level);
        }

        public override void SetupSkill()
        {
            base.SetupSkill();
            On.RoR2.HealthComponent.TakeDamageProcess += (orig, self, damageInfo) =>
            {
                // Fire damage against an oiled target: +15% per level of the attacker's Tectonic Blast.
                if (damageInfo != null && self && self.body && self.body.HasBuff(DLC2Content.Buffs.Oiled)
                    && DamageTypeCombo.IsFireSkillDamage(damageInfo.damageType)
                    && damageInfo.attacker && damageInfo.attacker.TryGetComponent(out CharacterBody attacker))
                {
                    int level = SkillUpgrade.GetLevel(attacker, SkillName);
                    if (level > 0) damageInfo.damage *= Mathf.Pow(1.15f, level);
                }
                orig(self, damageInfo);
            };
        }
    }

    /// <summary>Engineer – Laser Cutter: heals turrets and drones more and lets you move faster while cutting.</summary>
    [SkillLevelModifier("LaserCutter", typeof(EngiLaserCutter), typeof(EngiLaserCutter2), typeof(EngiLaserCutter3))]
    internal class EngiLaserCutterSkillModifier : BaseSkillModifier
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.06f;

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            if (skillState is EngiLaserCutter cutter)
            {
                cutter.healCoefficient = MultScaling(cutter.healCoefficient, 0.25f, level);
                cutter.walkSpeedCoefficient = Mathf.Min(1f, cutter.walkSpeedCoefficient + 0.10f * level);
                // Reach the next power stage faster (the final stage keeps its duration).
                if (!(skillState is EngiLaserCutter3) && cutter.baseMaxDuration > 0f)
                {
                    cutter.baseMaxDuration = MultScaling(cutter.baseMaxDuration, -0.15f, level);
                }
            }
        }
    }

    /// <summary>Acrid – Cloying Breath: the cloud lasts longer and is bigger.</summary>
    [SkillLevelModifier("CrocoBreath", typeof(Breath))]
    internal class CrocoBreathSkillModifier : SimpleSkillModifier<Breath>
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.12f;

        public override void OnSkillEnter(Breath skillState, int level)
        {
            GameObject prefab = skillState.projectilePrefab;
            ProjectileTweaks.Register(prefab, (projectile, owner, lvl) =>
            {
                if (projectile.TryGetComponent(out ProjectileDotZone zone))
                {
                    zone.lifetime += 1.5f * lvl;
                }
                projectile.transform.localScale *= 1f + 0.15f * lvl;
                projectile.AddComponent<SlowingCloud>().level = lvl;
            }, "CrocoBreath");
        }
    }

    /// <summary>
    /// Added to Cloying Breath clouds on the host: enemies inside are slowed by 50%, refreshed
    /// every half second while they stay in the cloud.
    /// </summary>
    internal class SlowingCloud : MonoBehaviour
    {
        internal int level;
        private float timer;
        private TeamIndex ownerTeam = TeamIndex.Player;
        private float radius = 5f;

        private void Start()
        {
            if (TryGetComponent(out ProjectileController controller) && controller.teamFilter) ownerTeam = controller.teamFilter.teamIndex;
            Collider collider = GetComponentInChildren<Collider>();
            if (collider) radius = Mathf.Max(radius, collider.bounds.extents.x);
        }

        private void FixedUpdate()
        {
            timer -= Time.fixedDeltaTime;
            if (timer > 0f) return;
            timer = 0.5f;
            BuffDef slow = RoR2Content.Buffs.Slow50;
            float radiusSqr = radius * radius;
            foreach (CharacterBody body in CharacterBody.readOnlyInstancesList)
            {
                if (!body || !body.teamComponent || body.teamComponent.teamIndex == ownerTeam) continue;
                if ((body.corePosition - transform.position).sqrMagnitude > radiusSqr) continue;
                body.AddTimedBuff(slow, 1f);
            }
        }
    }

    /// <summary>REX – Proliferation: cheaper flowers and a bigger heal when you teleport to one.</summary>
    [SkillLevelModifier("TreebotBodyProliferation", typeof(Proliferation), typeof(ProliferationTeleport))]
    internal class TreebotProliferationSkillModifier : BaseSkillModifier
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.10f;

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            switch (skillState)
            {
                case Proliferation proliferation:
                    proliferation.healthCostFraction = Mathf.Max(0.08f, proliferation.healthCostFraction - 0.03f * level);
                    break;
                case ProliferationTeleport teleport:
                    teleport.percentToHeal += 0.05f * level;
                    break;
            }
        }
    }
}
