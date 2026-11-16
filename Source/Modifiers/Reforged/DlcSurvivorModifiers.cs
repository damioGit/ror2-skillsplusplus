using EntityStates;
using EntityStates.Acolyte;
using EntityStates.Acolyte.Weapon;
using EntityStates.Drifter;
using EntityStates.Drifter.Bag;
using EntityStates.Drone.Command;
using EntityStates.DroneTech;
using EntityStates.DroneTech.Weapon;
using EntityStates.Enforcer;
using EntityStates.FalseSon;
using R2API;
using RoR2;
using RoR2.Skills;
using SkillsPlusPlus.Util;
using UnityEngine;
using UnityEngine.Networking;

// Survivors from Seekers of the Storm (False Son), Alloyed Collective (Drifter, Operator) and DLC4 (Acolyte,
// Enforcer). Every field below exists on the 1.5.0 entity state. Damage comes from the host-side slot bonus unless
// noted; these classes add each skill's mechanical upgrade.
namespace SkillsPlusPlus.Modifiers
{
    #region False Son

    [SkillLevelModifier("FalseSonBodyClub", typeof(ChargedClubSwing), typeof(ClubGroundSlam))]
    internal class FalseSonClubSkillModifier : BaseSkillModifier
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.06f;

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            if (skillState is ChargedClubSwing)
                ChargedClubSwing.blastRadius = MultScaling(Originals.Get("FS.ChargedClub.blastRadius", ChargedClubSwing.blastRadius), 0.15f, level);
            else if (skillState is ClubGroundSlam)
                ClubGroundSlam.blastRadius = MultScaling(Originals.Get("FS.ClubSlam.blastRadius", ClubGroundSlam.blastRadius), 0.15f, level);
        }
    }

    [SkillLevelModifier("FalseSonBodyLunarSpikes", typeof(LunarSpikes))]
    internal class FalseSonLunarSpikesSkillModifier : SimpleSkillModifier<LunarSpikes>
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.10f;

        public override void OnSkillLeveledUp(int level, CharacterBody characterBody, SkillDef skillDef)
        {
            base.OnSkillLeveledUp(level, characterBody, skillDef);
            skillDef.baseMaxStock = Originals.Get("FS.LunarSpikes.stock", skillDef.baseMaxStock) + level;
        }

        // Spikes also regenerate on their own: one every 4 s at level 1, 0.5 s faster per level (min 1.5 s).
        private sealed class Timer { public float value; }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<CharacterBody, Timer> timers =
            new System.Runtime.CompilerServices.ConditionalWeakTable<CharacterBody, Timer>();

        public override void SetupSkill()
        {
            base.SetupSkill();
            On.RoR2.CharacterBody.FixedUpdate += (orig, self) =>
            {
                orig(self);
                if (!self || !self.hasEffectiveAuthority || !self.isPlayerControlled || !self.skillLocator) return;
                GenericSkill secondary = self.skillLocator.secondary;
                if (!secondary || secondary.stock >= secondary.maxStock) return;
                if (!string.Equals(SkillUpgrade.SkillName(secondary.skillDef), "FalseSonBodyLunarSpikes", System.StringComparison.OrdinalIgnoreCase)) return;
                int level = SkillUpgrade.GetLevel(self, "FalseSonBodyLunarSpikes");
                if (level <= 0) return;

                Timer timer = timers.GetOrCreateValue(self);
                timer.value += Time.fixedDeltaTime;
                float interval = Mathf.Max(1.5f, 4.5f - 0.5f * level);
                if (timer.value >= interval)
                {
                    timer.value = 0f;
                    secondary.AddOneStock();
                }
            };
        }
    }

    [SkillLevelModifier("FalseSonLunarStake", typeof(LunarStake))]
    internal class FalseSonLunarStakeSkillModifier : SimpleSkillModifier<LunarStake>
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.10f;

        public override void OnSkillLeveledUp(int level, CharacterBody characterBody, SkillDef skillDef)
        {
            base.OnSkillLeveledUp(level, characterBody, skillDef);
            skillDef.baseMaxStock = Originals.Get("FS.LunarStake.stock", skillDef.baseMaxStock) + level / 2;
        }

        // Stake hits arc lightning to one more enemy per level (40% of the hit, host-side).
        public override void SetupSkill()
        {
            base.SetupSkill();
            On.RoR2.GlobalEventManager.OnHitEnemy += (orig, self, damageInfo, victim) =>
            {
                orig(self, damageInfo, victim);
                if (!NetworkServer.active || damageInfo == null || !damageInfo.attacker || !victim) return;
                if ((damageInfo.damageType.damageSource & DamageSource.Secondary) == 0) return;
                if (damageInfo.procChainMask.HasProc(ProcType.ChainLightning)) return;
                if (!damageInfo.attacker.TryGetComponent(out CharacterBody attacker) || !attacker.skillLocator || !attacker.skillLocator.secondary) return;
                if (!string.Equals(SkillUpgrade.SkillName(attacker.skillLocator.secondary.skillDef), "FalseSonLunarStake", System.StringComparison.OrdinalIgnoreCase)) return;
                int level = SkillUpgrade.GetLevel(attacker, "FalseSonLunarStake");
                if (level <= 0) return;

                var orb = new RoR2.Orbs.LightningOrb
                {
                    origin = damageInfo.position,
                    damageValue = damageInfo.damage * 0.4f,
                    isCrit = damageInfo.crit,
                    bouncesRemaining = level,
                    teamIndex = attacker.teamComponent ? attacker.teamComponent.teamIndex : TeamIndex.Player,
                    attacker = damageInfo.attacker,
                    procCoefficient = 0.2f,
                    lightningType = RoR2.Orbs.LightningOrb.LightningType.Ukulele,
                    damageColorIndex = DamageColorIndex.Item,
                    range = 20f,
                    damageType = new DamageTypeCombo(DamageType.Generic, DamageTypeExtended.Generic, DamageSource.NoneSpecified),
                };
                orb.procChainMask = damageInfo.procChainMask;
                orb.procChainMask.AddProc(ProcType.ChainLightning);
                orb.bouncedObjects = new System.Collections.Generic.List<HealthComponent>();
                if (victim.TryGetComponent(out HealthComponent victimHealth)) orb.bouncedObjects.Add(victimHealth);
                HurtBox target = orb.PickNextTarget(damageInfo.position);
                if (target)
                {
                    orb.target = target;
                    RoR2.Orbs.OrbManager.instance.AddOrb(orb);
                }
            };
        }
    }

    [SkillLevelModifier("FalseSonBodyStep", typeof(StepBrothers))]
    internal class FalseSonStepSkillModifier : SimpleSkillModifier<StepBrothers>
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.10f;

        public override void OnSkillEnter(StepBrothers skillState, int level)
        {
            StepBrothers.blastRadius = MultScaling(Originals.Get("FS.Step.blastRadius", StepBrothers.blastRadius), 0.20f, level);
        }

        public override void OnSkillLeveledUp(int level, CharacterBody characterBody, SkillDef skillDef)
        {
            base.OnSkillLeveledUp(level, characterBody, skillDef);
            skillDef.baseMaxStock = Originals.Get("FS.Step.stock", skillDef.baseMaxStock) + level / 2;
        }
    }

    [SkillLevelModifier("FalseSonMeridiansWill", typeof(MeridiansWillFire))]
    internal class FalseSonMeridiansWillSkillModifier : SimpleSkillModifier<MeridiansWillFire>
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.10f;

        public override void OnSkillEnter(MeridiansWillFire skillState, int level)
        {
            skillState.coreBuffDuration += 0.75f * level;
            skillState.forcePullRadius = MultScaling(skillState.forcePullRadius, 0.20f, level);
        }
    }

    [SkillLevelModifier("FalseSonBodyLaser", typeof(LaserFather), typeof(LaserFatherCharged))]
    internal class FalseSonLaserSkillModifier : BaseSkillModifier
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.12f;

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            if (skillState is LaserFather laser)
                laser.baseChargeDuration = MultScaling(laser.baseChargeDuration, -0.15f, level);
            else if (skillState is LaserFatherCharged)
                LaserFatherCharged.maximumDuration = Originals.Get("FS.Laser.maxDuration", LaserFatherCharged.maximumDuration) + 0.5f * level;
        }
    }

    [SkillLevelModifier("FalseSonBodyLaserBurst", typeof(LaserFatherBurstPrep), typeof(LaserFatherBurst))]
    internal class FalseSonLaserBurstSkillModifier : BaseSkillModifier
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.15f;

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            if (skillState is LaserFatherBurstPrep prep)
                prep.secondaryRefillPercentage = Mathf.Min(1f, prep.secondaryRefillPercentage + 0.12f * level);
        }
    }

    #endregion

    #region Drifter

    [SkillLevelModifier("BluntForce", new System.Type[0])]
    internal class DrifterBluntForceSkillModifier : BaseSkillModifier
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.08f;
    }

    [SkillLevelModifier("Cleanup", typeof(Cleanup))]
    internal class DrifterCleanupSkillModifier : SimpleSkillModifier<Cleanup>
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.10f;

        public override void OnSkillEnter(Cleanup skillState, int level)
        {
            skillState.numShotgunProjectiles += level;
        }
    }

    [SkillLevelModifier("JunkCube", typeof(JunkCube), typeof(CubeSlam))]
    internal class DrifterJunkCubeSkillModifier : BaseSkillModifier
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.10f;

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            if (skillState is CubeSlam slam)
                slam.junkDropChance = Mathf.Min(1f, slam.junkDropChance + 0.05f * level);
        }
    }

    [SkillLevelModifier(new[] { "Repossess", "Discard" }, typeof(Repossess), typeof(BaggedObject))]
    internal class DrifterRepossessSkillModifier : BaseSkillModifier
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.10f;

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            if (skillState is BaggedObject bagged)
            {
                // Monsters take longer to break out of the bag.
                bagged.baseBreakoutTime = MultScaling(bagged.baseBreakoutTime, 0.25f, level);
            }
        }
    }

    [SkillLevelModifier("TornadoSlam", typeof(TornadoSlam), typeof(ChargeTornadoSlam))]
    internal class DrifterTornadoSlamSkillModifier : BaseSkillModifier
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.10f;

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            if (skillState is TornadoSlam tornado)
                tornado.extraDurationOnBounce = MultScaling(tornado.extraDurationOnBounce, 0.25f, level);
        }
    }

    #endregion

    #region Operator (Drone Tech)

    [SkillLevelModifier("FireNanoPistol", typeof(FireNanoPistol))]
    internal class OperatorPistolSkillModifier : SimpleSkillModifier<FireNanoPistol>
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.06f;
    }

    /// <summary>ADMIN-OVERRIDE: shorter cooldown.</summary>
    [SkillLevelModifier("Command", typeof(Activate))]
    internal class OperatorAdminOverrideSkillModifier : SimpleSkillModifier<Activate>
    {
        public override void SetupSkill()
        {
            base.SetupSkill();
            RecalculateStatsAPI.GetStatCoefficients += (sender, args) =>
            {
                int level = SkillUpgrade.GetLevel(sender, "Command");
                if (level > 0) args.secondaryCooldownMultAdd -= 1f - Mathf.Pow(0.92f, level);
            };
            // One extra charge every 2 levels, on top of one per drone. The game resets secondary bonus stock
            // in every RecalculateStats, so adding after it is stable.
            On.RoR2.CharacterBody.RecalculateStats += (orig, self) =>
            {
                orig(self);
                if (!self || !self.skillLocator || !self.skillLocator.secondary) return;
                GenericSkill secondary = self.skillLocator.secondary;
                if (!string.Equals(SkillUpgrade.SkillName(secondary.skillDef), "Command", System.StringComparison.OrdinalIgnoreCase)) return;
                int bonus = SkillUpgrade.GetLevel(self, "Command") / 2;
                if (bonus > 0) secondary.SetBonusStockFromBody(secondary.bonusStockFromBody + bonus);
            };
        }
    }

    /// <summary>CMD-SWARM: drone rams hit harder, in a bigger area and shock longer. Drones are minions, so damage is set on the state.</summary>
    [SkillLevelModifier("CommandHeadbutt", typeof(Headbutt), typeof(CommandHeadbutt))]
    internal class OperatorSwarmSkillModifier : BaseSkillModifier
    {
        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            if (skillState is Headbutt headbutt)
            {
                headbutt.damageCoefficient = MultScaling(headbutt.damageCoefficient, 0.10f, level);
                headbutt.blastRadius = MultScaling(headbutt.blastRadius, 0.15f, level);
                headbutt.shockDuration += 0.3f * level;
            }
            else if (skillState is CommandHeadbutt command)
            {
                command.damageCoefficient = MultScaling(command.damageCoefficient, 0.10f, level);
                command.blastRadius = MultScaling(command.blastRadius, 0.15f, level);
            }
        }
    }

    [SkillLevelModifier("DroneLeap", typeof(DroneLeap))]
    internal class OperatorAscentSkillModifier : SimpleSkillModifier<DroneLeap>
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.10f;

        public override void OnSkillEnter(DroneLeap skillState, int level)
        {
            skillState.upwardVelocity = MultScaling(skillState.upwardVelocity, 0.10f, level);
            skillState.forwardVelocity = MultScaling(skillState.forwardVelocity, 0.10f, level);
        }

        /// <summary>Landing from Ascent Protocol sends out a shockwave: 150% damage per level in 8 m (host-side).</summary>
        public override void OnSkillExit(DroneLeap skillState, int level)
        {
            CharacterBody body = skillState.characterBody;
            if (!NetworkServer.active || level <= 0 || !body || !skillState.isGrounded || !body.teamComponent) return;
            new BlastAttack
            {
                attacker = body.gameObject,
                inflictor = body.gameObject,
                teamIndex = body.teamComponent.teamIndex,
                position = body.footPosition,
                radius = 8f,
                baseDamage = body.damage * 1.5f * level,
                baseForce = 800f,
                crit = body.RollCrit(),
                procCoefficient = 0.5f,
                falloffModel = BlastAttack.FalloffModel.Linear,
                attackerFiltering = AttackerFiltering.NeverHitSelf,
                damageColorIndex = DamageColorIndex.Default,
                damageType = new DamageTypeCombo(DamageType.Generic, DamageTypeExtended.Generic, DamageSource.Utility),
            }.Fire();
            EffectManager.SpawnEffect(LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/PodGroundImpact"),
                new EffectData { origin = body.footPosition, scale = 8f }, transmit: true);
        }
    }

    [SkillLevelModifier("CommandShieldFormation", typeof(ShieldFormation), typeof(FireShield))]
    internal class OperatorFirewallSkillModifier : BaseSkillModifier
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.12f;

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            if (skillState is ShieldFormation shield)
                shield.shieldDuration = MultScaling(shield.shieldDuration, 0.20f, level);
        }
    }

    [SkillLevelModifier(new[] { "DroneBall", "DroneBallShootable" }, typeof(ThrowDroneBall))]
    internal class OperatorCoreSkillModifier : SimpleSkillModifier<ThrowDroneBall>
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.12f;
    }

    #endregion

    #region Acolyte

    /// <summary>Blade Slice: quickstrikes (jump/sprint strikes) can hit one more time every 2 levels.</summary>
    [SkillLevelModifier("BladeCombo", typeof(BladeCombo), typeof(BladeJumpStrike), typeof(BladeSprintStrike))]
    internal class AcolyteBladeComboSkillModifier : BaseSkillModifier
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.06f;

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            if (skillState is BaseBladeComboRepeating repeating)
                repeating.maxHitCount += level / 2;
        }
    }

    /// <summary>Bola Throw: gathers one more enemy per level (vanilla 5). The gather runs on the bola projectile.</summary>
    [SkillLevelModifier("Bola", typeof(BolaWhip), typeof(RoR2.Projectile.BolaProjectileController.GatherState))]
    internal class AcolyteBolaSkillModifier : BaseSkillModifier
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.10f;

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            if (skillState is RoR2.Projectile.BolaProjectileController.GatherState)
            {
                RoR2.Projectile.BolaProjectileController.GatherState.maxTargets =
                    Originals.Get("Acolyte.Bola.maxTargets", RoR2.Projectile.BolaProjectileController.GatherState.maxTargets) + level;
            }
        }
    }

    /// <summary>Soul Jar: haunted enemies release one more wight per level when they die.</summary>
    [SkillLevelModifier("AimSoulJar", typeof(AimSoulJar))]
    internal class AcolyteSoulJarSkillModifier : SimpleSkillModifier<AimSoulJar>
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.10f;

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<WightLauncher, object> boosted =
            new System.Runtime.CompilerServices.ConditionalWeakTable<WightLauncher, object>();

        public override void SetupSkill()
        {
            base.SetupSkill();
            On.RoR2.WightLauncher.FixedUpdate += (orig, self) =>
            {
                // First tick of a new launcher (spawned on the host when a haunted enemy dies).
                if (NetworkServer.active && self.owner && self.remaining > 0 && !boosted.TryGetValue(self, out _))
                {
                    boosted.Add(self, null);
                    int level = SkillUpgrade.GetLevel(self.owner.GetComponent<CharacterBody>(), "AimSoulJar");
                    if (level > 0) self.remaining += level;
                }
                orig(self);
            };
        }
    }

    /// <summary>Blade Sling: dashing to the blade grants a short invulnerability window.</summary>
    [SkillLevelModifier("BladeSling", typeof(BladeSling), typeof(BladeDash))]
    internal class AcolyteBladeSlingSkillModifier : BaseSkillModifier
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.10f;

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            if (skillState is BladeDash && NetworkServer.active && skillState.characterBody)
                skillState.characterBody.AddTimedBuff(RoR2Content.Buffs.HiddenInvincibility, 0.3f * level);
        }
    }

    /// <summary>Javelin: 20% bigger explosion per level.</summary>
    [SkillLevelModifier("ThrowJavelin", typeof(ThrowJavelin))]
    internal class AcolyteJavelinSkillModifier : SimpleSkillModifier<ThrowJavelin>
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.10f;

        public override void OnSkillEnter(ThrowJavelin skillState, int level)
        {
            ProjectileTweaks.Register(skillState.projectilePrefab, (projectile, owner, lvl) =>
            {
                foreach (var explosion in projectile.GetComponents<RoR2.Projectile.ProjectileExplosion>())
                {
                    explosion.blastRadius *= 1f + 0.20f * lvl;
                }
            }, "ThrowJavelin");
        }
    }

    /// <summary>Sacrifice: an extra Conviction every 2 levels, so the Litany comes sooner.</summary>
    [SkillLevelModifier("Sacrifice", typeof(EntityStates.Acolyte.Weapon.Sacrifice))]
    internal class AcolyteSacrificeSkillModifier : SimpleSkillModifier<EntityStates.Acolyte.Weapon.Sacrifice>
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.12f;

        public override void OnSkillEnter(EntityStates.Acolyte.Weapon.Sacrifice skillState, int level)
        {
            skillState.soulValue += level / 2;
        }
    }

    #endregion

    #region Enforcer

    [SkillLevelModifier("EnforcerRiotShotgun", typeof(FireRiotShotgun), typeof(FireRiotShotgunShielded))]
    internal class EnforcerRiotShotgunSkillModifier : BaseSkillModifier
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.06f;

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            // Damage falloff starts 5 m later per level.
            FireRiotShotgun.falloffStartDistance = Originals.Get("Enf.Riot.falloffStart", FireRiotShotgun.falloffStartDistance) + 5f * level;
            FireRiotShotgun.falloffEndDistance = Originals.Get("Enf.Riot.falloffEnd", FireRiotShotgun.falloffEndDistance) + 5f * level;
            FireRiotShotgunShielded.falloffStartDistance = Originals.Get("Enf.RiotS.falloffStart", FireRiotShotgunShielded.falloffStartDistance) + 5f * level;
            FireRiotShotgunShielded.falloffEndDistance = Originals.Get("Enf.RiotS.falloffEnd", FireRiotShotgunShielded.falloffEndDistance) + 5f * level;
        }
    }

    [SkillLevelModifier("EnforcerClusterGrenade", new System.Type[0])]
    internal class EnforcerClusterGrenadeSkillModifier : BaseSkillModifier
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.10f;
    }

    [SkillLevelModifier("EnforcerProtectAndServeActivate", typeof(ProtectAndServeActivate), typeof(ProtectAndServeDeactivate))]
    internal class EnforcerSteelProtectionSkillModifier : BaseSkillModifier
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.10f;

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            if (skillState is ProtectAndServeBaseState stance)
                stance.walkSpeedPenalty = MultScaling(stance.walkSpeedPenalty, -0.10f, level);
            if (skillState is ProtectAndServeDeactivate deactivate)
                deactivate.deflectDamageMultiplier = MultScaling(deactivate.deflectDamageMultiplier, 0.20f, level);
        }

        // The first successful parry of each block refunds 0.5 s of utility cooldown per level.
        public override void SetupSkill()
        {
            base.SetupSkill();
            On.EntityStates.Enforcer.ProtectAndServeDeactivate.TriggerParry += (orig, self, vfxOrigin) =>
            {
                bool firstParry = self.parryCount == 0;
                orig(self, vfxOrigin);
                if (!firstParry || !self.isAuthority || !self.characterBody || !self.characterBody.skillLocator) return;
                int level = SkillUpgrade.GetLevel(self.characterBody, "EnforcerProtectAndServeActivate");
                GenericSkill utility = self.characterBody.skillLocator.utility;
                if (level > 0 && utility) utility.RunRecharge(0.5f * level);
            };
        }
    }

    /// <summary>Surge Protection: lasts longer, absorbs 12% more of your max HP per level before it pops, bigger explosion.</summary>
    [SkillLevelModifier("EnforcerKineticCharge2", typeof(KineticCharge2Activate), typeof(KineticCharge2ActivateDamageLimited), typeof(KineticChargeExplode))]
    internal class EnforcerSurgeProtectionSkillModifier : BaseSkillModifier
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.12f;

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            if (skillState is KineticCharge2Activate)
            {
                KineticCharge2Activate.durationLimit = Originals.Get("Enf.Surge.duration", KineticCharge2Activate.durationLimit) + 1f * level;
                KineticCharge2Activate.damageLimitFraction = Originals.Get("Enf.Surge.damageLimit", KineticCharge2Activate.damageLimitFraction) + 0.12f * level;
            }
            else if (skillState is KineticChargeExplode)
                KineticChargeExplode.maxRadius = MultScaling(Originals.Get("Enf.Surge.maxRadius", KineticChargeExplode.maxRadius), 0.15f, level);
        }
    }

    [SkillLevelModifier("EnforcerShieldTackleCharged", typeof(BaseShieldDashPrepCharge))]
    internal class EnforcerShieldTackleSkillModifier : SimpleSkillModifier<BaseShieldDashPrepCharge>
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.10f;

        public override void OnSkillEnter(BaseShieldDashPrepCharge skillState, int level)
        {
            skillState.chargeLevelSpeedCoefficient = MultScaling(skillState.chargeLevelSpeedCoefficient, 0.12f, level);
        }
    }

    [SkillLevelModifier("EnforcerTearGas", typeof(FireTearGasGrenade))]
    internal class EnforcerRipcordSkillModifier : SimpleSkillModifier<FireTearGasGrenade>
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.10f;
    }

    [SkillLevelModifier("EnforcerCrowdControlDef", typeof(CrowdControlSlamGround))]
    internal class EnforcerRallyPointSkillModifier : SimpleSkillModifier<CrowdControlSlamGround>
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.12f;

        public override void OnSkillEnter(CrowdControlSlamGround skillState, int level)
        {
            CrowdControlSlamGround.blastRadius = MultScaling(Originals.Get("Enf.Rally.blastRadius", CrowdControlSlamGround.blastRadius), 0.20f, level);
        }
    }

    [SkillLevelModifier("ShieldLeap", typeof(ShieldLeap), typeof(ShieldSlamImpact))]
    internal class EnforcerCrashPointSkillModifier : BaseSkillModifier
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.20f;

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            if (skillState is ShieldSlamImpact impact)
            {
                impact.slamRadius = MultScaling(impact.slamRadius, 0.15f, level);
                SlowAround(impact.characterBody, impact.slamRadius * 1.5f);
            }
        }

        /// <summary>Host-side: slows enemies around the impact by 50% for 2 seconds.</summary>
        private static void SlowAround(CharacterBody body, float radius)
        {
            if (!NetworkServer.active || !body || !body.teamComponent) return;
            Vector3 center = body.footPosition;
            float radiusSqr = radius * radius;
            foreach (CharacterBody other in CharacterBody.readOnlyInstancesList)
            {
                if (!other || !other.teamComponent || other.teamComponent.teamIndex == body.teamComponent.teamIndex) continue;
                if (!other.healthComponent || !other.healthComponent.alive) continue;
                if ((other.corePosition - center).sqrMagnitude > radiusSqr) continue;
                other.AddTimedBuff(RoR2Content.Buffs.Slow50, 2f);
            }
        }
    }

    #endregion
}
