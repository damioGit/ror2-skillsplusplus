using EntityStates;
using EntityStates.Railgunner.Backpack;
using EntityStates.Railgunner.Reload;
using EntityStates.Railgunner.Scope;
using EntityStates.Railgunner.Weapon;
using R2API;
using RoR2;
using RoR2.Projectile;
using RoR2.Skills;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using static R2API.RecalculateStatsAPI;

namespace SkillsPlusPlus.Modifiers
{
    [SkillLevelModifier("RailgunnerBodyFirePistol", typeof(FirePistol))]
    internal class RailgunnerXQRSkillModifier : SimpleSkillModifier<FirePistol>
    {
        private float originalDuration = 0;
        private float originalLifetime = 0;

        public override void OnSkillEnter(FirePistol skillState, int level)
        {
            if (Mathf.Abs(originalDuration) < 0.01f)
            {
                originalDuration = skillState.baseDuration;

                ProjectileSimple prefabSimple = skillState.projectilePrefab.GetComponent<ProjectileSimple>();
                if (prefabSimple)
                {
                    originalLifetime = prefabSimple.lifetime;
                }
                else
                {
                    SkillsPlusPlus.Logger.Error("Failed to find the Railgunner ProjectilePrefab somehow");
                }
            }

            skillState.baseDuration = MultScaling(originalDuration, -0.10f, level);

            {
                ProjectileSimple prefabSimple = skillState.projectilePrefab.GetComponent<ProjectileSimple>();
                if (prefabSimple)
                {
                    prefabSimple.lifetime = MultScaling(originalLifetime, 0.20f, level);
                }
            }

            base.OnSkillEnter(skillState, level);
        }

        public override void OnSkillLeveledUp(int level, CharacterBody characterBody, SkillDef skillDef)
        {
            base.OnSkillLeveledUp(level, characterBody, skillDef);
        }
    }


    // All Railgunner modifiers below keep their state per body (dictionaries / sets keyed by CharacterBody) and read
    // levels per player, instead of the single static "registered skill" they used before. Skill states run on both
    // the owning client and the host, so scope/cryo tracking also works on the host for clients.

    [SkillLevelModifier("RailgunnerBodyScopeHeavy", typeof(FireSnipeHeavy))]
    internal class RailgunnerM99SkillModifier : SimpleSkillModifier<FireSnipeHeavy>
    {
        private const string SkillName = "RailgunnerBodyScopeHeavy";
        private static readonly Dictionary<CharacterBody, int> empoweredRoundsLeft = new Dictionary<CharacterBody, int>();

        public override void OnSkillLeveledUp(int level, CharacterBody characterBody, SkillDef skillDef)
        {
            base.OnSkillLeveledUp(level, characterBody, skillDef);
            skillDef.baseMaxStock = 1 + level / 2;
        }

        public override void SetupSkill()
        {
            On.EntityStates.Railgunner.Reload.Boosted.ConsumeBoost += ConsumeBoost;
            On.EntityStates.Railgunner.Reload.BoostConfirm.OnEnter += OnEnterBoostConfirm;
            base.SetupSkill();
        }

        private static void ConsumeBoost(On.EntityStates.Railgunner.Reload.Boosted.orig_ConsumeBoost orig, Boosted self, bool queueReload)
        {
            CharacterBody body = self.characterBody;
            // Do not waste the empowered round if we have more left to shoot.
            if (body && empoweredRoundsLeft.TryGetValue(body, out int left) && --left > 0)
            {
                empoweredRoundsLeft[body] = left;
                RoR2.Util.PlaySound(self.boostConsumeSoundString, self.gameObject);
                return;
            }
            if (body) empoweredRoundsLeft.Remove(body);
            orig(self, queueReload);
        }

        private static void OnEnterBoostConfirm(On.EntityStates.Railgunner.Reload.BoostConfirm.orig_OnEnter orig, BoostConfirm self)
        {
            CharacterBody body = self.characterBody;
            int level = SkillUpgrade.GetLevel(body, SkillName);
            if (body && level > 0)
            {
                int maxStock = body.skillLocator && body.skillLocator.secondary ? body.skillLocator.secondary.maxStock : 1;
                empoweredRoundsLeft[body] = Math.Min(maxStock, level + 1);
            }
            orig(self);
        }
    }

    [SkillLevelModifier("RailgunnerBodyScopeLight", typeof(FireSnipeLight), typeof(WindUpScopeLight), typeof(WindDownScopeLight))]
    internal class RailgunnerHH44SkillModifier : BaseSkillModifier
    {
        private const string SkillName = "RailgunnerBodyScopeLight";
        private const float SpreeTimerGrace = 2f;

        private static BuffDef KillingSpreeBuff;
        private static BuffDef StrategicRetreatBuff;
        private static readonly HashSet<CharacterBody> scopedBodies = new HashSet<CharacterBody>();
        private static readonly Dictionary<CharacterBody, float> spreeTimers = new Dictionary<CharacterBody, float>();

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            CharacterBody body = skillState.outer.commonComponents.characterBody;
            if (skillState is WindUpScopeLight)
            {
                if (body) scopedBodies.Add(body);
            }
            else if (skillState is WindDownScopeLight)
            {
                if (body) scopedBodies.Remove(body);
                OnWindDownScopeLightEnter(body, level);
            }
            base.OnSkillEnter(skillState, level);
        }

        private static void OnWindDownScopeLightEnter(CharacterBody body, int level)
        {
            // Buffs only live on the host; the host runs this state for every Railgunner.
            if (!NetworkServer.active || !body || !body.HasBuff(KillingSpreeBuff)) return;

            int killingSpree = body.GetBuffCount(KillingSpreeBuff);
            body.SetBuffCount(KillingSpreeBuff.buffIndex, 0);
            spreeTimers.Remove(body);

            float retreatDuration = 0.25f * level * killingSpree;
            foreach (CharacterBody.TimedBuff buff in body.timedBuffs)
            {
                if (buff.buffIndex == StrategicRetreatBuff.buffIndex)
                {
                    buff.timer += retreatDuration;
                    return;
                }
            }
            body.AddTimedBuff(StrategicRetreatBuff, retreatDuration);
        }

        private void RegisterKillingBuffs()
        {
            {
                BuffDef buffDef = ScriptableObject.CreateInstance<BuffDef>();
                buffDef.buffColor = new Color(0.78f, 0.20f, 0.78f);
                buffDef.canStack = true;
                buffDef.eliteDef = null;
                buffDef.iconSprite = Addressables.LoadAssetAsync<BuffDef>("RoR2/Base/CritOnUse/bdFullCrit.asset").WaitForCompletion().iconSprite;
                buffDef.isDebuff = true;
                buffDef.name = "RailgunnerKillingSpreeBuff";
                KillingSpreeBuff = buffDef;
                ContentAddition.AddBuffDef(buffDef);
            }
            {
                BuffDef buffDef = ScriptableObject.CreateInstance<BuffDef>();
                buffDef.buffColor = new Color(0.74f, 0.36f, 0.23f);
                buffDef.canStack = false;
                buffDef.eliteDef = null;
                buffDef.iconSprite = Addressables.LoadAssetAsync<BuffDef>("RoR2/DLC1/MoveSpeedOnKill/bdKillMoveSpeed.asset").WaitForCompletion().iconSprite;
                buffDef.isDebuff = true;
                buffDef.name = "RailgunnerStrategicRetreatBuff";
                StrategicRetreatBuff = buffDef;
                ContentAddition.AddBuffDef(buffDef);
            }
        }

        private static void GlobalEventManager_OnCharacterDeath(On.RoR2.GlobalEventManager.orig_OnCharacterDeath orig, RoR2.GlobalEventManager self, RoR2.DamageReport damageReport)
        {
            CharacterBody attacker = damageReport?.attackerBody;
            if (attacker && scopedBodies.Contains(attacker) && SkillUpgrade.GetLevel(attacker, SkillName) > 0)
            {
                attacker.AddBuff(KillingSpreeBuff);
                spreeTimers[attacker] = SpreeTimerGrace;
            }
            orig(self, damageReport);
        }

        private static void CharacterBody_FixedUpdate(On.RoR2.CharacterBody.orig_FixedUpdate orig, RoR2.CharacterBody self)
        {
            orig(self);
            if (!NetworkServer.active || !self || !spreeTimers.TryGetValue(self, out float timer)) return;
            if (!self.HasBuff(KillingSpreeBuff))
            {
                spreeTimers.Remove(self);
                return;
            }
            timer -= Time.fixedDeltaTime;
            if (timer < 0f)
            {
                timer += SpreeTimerGrace;
                int count = self.GetBuffCount(KillingSpreeBuff);
                self.SetBuffCount(KillingSpreeBuff.buffIndex, Math.Max(0, count - 1));
            }
            spreeTimers[self] = timer;
        }

        private static void RecalculateStats(CharacterBody sender, StatHookEventArgs args)
        {
            if (!sender) return;
            if (sender.HasBuff(StrategicRetreatBuff))
            {
                args.baseMoveSpeedAdd += 3f;
            }
            if (sender.HasBuff(KillingSpreeBuff))
            {
                args.armorAdd += sender.GetBuffCount(KillingSpreeBuff) * SkillUpgrade.GetLevel(sender, SkillName);
            }
        }

        public override void SetupSkill()
        {
            RegisterKillingBuffs();
            On.RoR2.GlobalEventManager.OnCharacterDeath += GlobalEventManager_OnCharacterDeath;
            On.RoR2.CharacterBody.FixedUpdate += CharacterBody_FixedUpdate;
            GetStatCoefficients += RecalculateStats;
            base.SetupSkill();
        }
    }

    [SkillLevelModifier("RailgunnerBodyFireMineConcussive", typeof(FireMineConcussive))]
    internal class RailgunnerConcussiveMineSkillModifier : SimpleSkillModifier<FireMineConcussive>
    {
        private const string SkillName = "RailgunnerBodyFireMineConcussive";
        private static BuffDef FeatherFallBuff;
        private static int ConcussiveProjectileCatalogIndex = -1337;
        private static float baseThrowForce = 0;
        // Level of the Railgunner whose mine launched each body, so the fall speed cap matches that player.
        private static readonly Dictionary<CharacterBody, int> featherFallLevel = new Dictionary<CharacterBody, int>();

        public override void OnSkillEnter(FireMineConcussive skillState, int level)
        {
            if (ConcussiveProjectileCatalogIndex == -1337)
            {
                if (skillState.projectilePrefab.TryGetComponent(out ProjectileController projectileController))
                {
                    ConcussiveProjectileCatalogIndex = projectileController.catalogIndex;
                }
                if (skillState.projectilePrefab.TryGetComponent(out ProjectileSimple projectileSimple))
                {
                    baseThrowForce = projectileSimple.desiredForwardSpeed;
                }
            }
            if (skillState.projectilePrefab.TryGetComponent(out ProjectileSimple projectile))
            {
                projectile.desiredForwardSpeed = MultScaling(baseThrowForce, 0.15f, level);
            }
            base.OnSkillEnter(skillState, level);
        }

        public override void SetupSkill()
        {
            RegisterFeatherFallBuff();
            On.RoR2.HealthComponent.TakeDamage += HealthComponent_TakeDamage;
            On.RoR2.CharacterBody.FixedUpdate += CharacterBody_FixedUpdate;
            base.SetupSkill();
        }

        private void RegisterFeatherFallBuff()
        {
            BuffDef buffDef = ScriptableObject.CreateInstance<BuffDef>();
            buffDef.buffColor = new Color(1f, 0.8f, 0.55f);
            buffDef.canStack = false;
            buffDef.eliteDef = null;
            buffDef.iconSprite = Addressables.LoadAssetAsync<BuffDef>("RoR2/Base/Bandit2/bdCloakSpeed.asset").WaitForCompletion().iconSprite;
            buffDef.isDebuff = false;
            buffDef.name = "RailgunnerFeatherFallBuff";
            FeatherFallBuff = buffDef;
            ContentAddition.AddBuffDef(buffDef);
        }

        private static void HealthComponent_TakeDamage(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo di)
        {
            if (self && self.body && di != null && di.inflictor && di.inflictor.TryGetComponent(out ProjectileController controller) && controller.catalogIndex == ConcussiveProjectileCatalogIndex)
            {
                CharacterBody attacker = di.attacker ? di.attacker.GetComponent<CharacterBody>() : null;
                int level = SkillUpgrade.GetLevel(attacker, SkillName);
                if (level > 0)
                {
                    featherFallLevel[self.body] = level;
                    self.body.AddTimedBuff(FeatherFallBuff, 1.0f + (0.5f * level));
                    if (self.body != attacker)
                    {
                        self.body.characterMotor?.AddDisplacement(new Vector3(0f, 5f, 0f));
                    }
                }
            }
            orig(self, di);
        }

        private static void CharacterBody_FixedUpdate(On.RoR2.CharacterBody.orig_FixedUpdate orig, CharacterBody self)
        {
            if (self && self.characterMotor && self.HasBuff(FeatherFallBuff) && !self.characterMotor.isGrounded)
            {
                int level = featherFallLevel.TryGetValue(self, out int stored) ? stored : 1;
                float upVelocity = Mathf.Max(self.characterMotor.velocity.y, -30f * (3.5f / (level + 3.5f)));
                self.characterMotor.velocity = new Vector3(self.characterMotor.velocity.x, upVelocity, self.characterMotor.velocity.z);
            }
            orig(self);
        }
    }

    [SkillLevelModifier("RailgunnerBodyFireMineBlinding", typeof(FireMineBlinding))]
    internal class RailgunnerPolarFieldMineSkillModifier : SimpleSkillModifier<FireMineBlinding>
    {
        private const string SkillName = "RailgunnerBodyFireMineBlinding";
        private static BuffDef TimePressureDebuff;
        private static int PolarProjectileCatalogIndex = -1337;
        private static float baseThrowForce = 0;
        private static float baseRadius = 0;
        private static readonly List<BuffWard> Wards = new List<BuffWard>();

        public override void OnSkillEnter(FireMineBlinding skillState, int level)
        {
            FirstTimeSetup(skillState);
            if (skillState.projectilePrefab.TryGetComponent(out ProjectileSimple projectile))
            {
                projectile.desiredForwardSpeed = MultScaling(baseThrowForce, 0.15f, level);
            }
            base.OnSkillEnter(skillState, level);
        }

        private static void FirstTimeSetup(FireMineBlinding skillState)
        {
            if (PolarProjectileCatalogIndex != -1337) return;

            if (skillState.projectilePrefab.TryGetComponent(out ProjectileExplosion projectileExplosion)
                && projectileExplosion.childrenProjectilePrefab.TryGetComponent(out BuffWard buffWard))
            {
                baseRadius = buffWard.radius;
                BuffWard copy = (BuffWard)projectileExplosion.childrenProjectilePrefab.AddComponent(buffWard.GetType());
                Wards.Add(buffWard);
                Wards.Add(copy);
                foreach (System.Reflection.FieldInfo field in buffWard.GetType().GetFields())
                {
                    field.SetValue(copy, field.GetValue(buffWard));
                }
            }
            if (skillState.projectilePrefab.TryGetComponent(out ProjectileController projectileController))
            {
                PolarProjectileCatalogIndex = projectileController.catalogIndex;
            }
            if (skillState.projectilePrefab.TryGetComponent(out ProjectileSimple projectileSimple))
            {
                baseThrowForce = projectileSimple.desiredForwardSpeed;
            }
        }

        public override void OnSkillLeveledUp(int level, CharacterBody characterBody, SkillDef skillDef)
        {
            base.OnSkillLeveledUp(level, characterBody, skillDef);
            bool isCopy = false;
            foreach (BuffWard ward in Wards)
            {
                ward.radius = MultScaling(baseRadius, 0.15f, level);
                if (isCopy && level > 0)
                {
                    ward.buffDef = TimePressureDebuff;
                }
                isCopy = true;
            }
        }

        public override void SetupSkill()
        {
            RegisterTimePressureDebuff();
            On.RoR2.HealthComponent.TakeDamage += HealthComponent_TakeDamage;
            base.SetupSkill();
        }

        private static void HealthComponent_TakeDamage(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo di)
        {
            if (self && self.body && di != null && self.body.HasBuff(TimePressureDebuff))
            {
                // The ward can't tell us which Railgunner placed it, so use the strongest one in the party.
                di.damage = MultScaling(di.damage, 0.1f, SkillUpgrade.GetHighestLevelAmongPlayers(SkillName));
            }
            orig(self, di);
        }

        private void RegisterTimePressureDebuff()
        {
            BuffDef buffDef = ScriptableObject.CreateInstance<BuffDef>();
            buffDef.buffColor = new Color(0.5f, 0.6f, 1f);
            buffDef.canStack = false;
            buffDef.eliteDef = null;
            buffDef.iconSprite = Addressables.LoadAssetAsync<BuffDef>("RoR2/Base/Grandparent/bdOverheat.asset").WaitForCompletion().iconSprite;
            buffDef.isDebuff = true;
            buffDef.name = "RailgunnerTimePressureBuff";
            TimePressureDebuff = buffDef;
            ContentAddition.AddBuffDef(buffDef);
        }
    }

    [SkillLevelModifier("RailgunnerBodyChargeSnipeSuper", typeof(FireSnipeSuper))]
    internal class RailgunnerSuperchargeSkillModifier : SimpleSkillModifier<FireSnipeSuper>
    {
        private const string SkillName = "RailgunnerBodyChargeSnipeSuper";
        private static float originalProcRate = 0f;
        private static float originalCritMult = 0f;

        public override void OnSkillEnter(FireSnipeSuper skillState, int level)
        {
            if (Mathf.Abs(originalProcRate) < 0.1f)
            {
                originalProcRate = skillState.procCoefficient;
                originalCritMult = skillState.critDamageMultiplier;
            }
            if (level > 0)
            {
                skillState.procCoefficient = MultScaling(originalProcRate, 0.1f, level);
                skillState.critDamageMultiplier = MultScaling(originalCritMult, 0.2f, level);
            }
            base.OnSkillEnter(skillState, level);
        }

        public override void SetupSkill()
        {
            GetStatCoefficients += RecalculateStats;
            base.SetupSkill();
        }

        // Previously only ever applied to player 1.
        private static void RecalculateStats(CharacterBody sender, StatHookEventArgs args)
        {
            int level = SkillUpgrade.GetLevel(sender, SkillName);
            if (level <= 0) return;
            args.utilityCooldownMultAdd -= 1 - (20f / (level + 20f));
            args.specialCooldownMultAdd -= 1 - (5f / (level + 5f));
        }
    }

    [SkillLevelModifier("RailgunnerBodyChargeSnipeCryo", typeof(FireSnipeCryo), typeof(ExpiredCryo), typeof(ChargedCryo))]
    internal class RailgunnerCryochargeSkillModifier : BaseSkillModifier
    {
        private const string SkillName = "RailgunnerBodyChargeSnipeCryo";
        private static BuffDef FrostfireBuff;
        private static DotController.DotIndex FrostfireIndex;
        private static readonly HashSet<CharacterBody> cryoActiveBodies = new HashSet<CharacterBody>();

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            if (skillState is ChargedCryo)
            {
                CharacterBody body = skillState.outer.commonComponents.characterBody;
                if (body) cryoActiveBodies.Add(body);
            }
        }

        public override void OnSkillExit(BaseState skillState, int level)
        {
            base.OnSkillExit(skillState, level);
            if (skillState is FireSnipeCryo || skillState is ExpiredCryo)
            {
                CharacterBody body = skillState.outer.commonComponents.characterBody;
                if (body) cryoActiveBodies.Remove(body);
            }
        }

        private void RegisterFrostfireBuff()
        {
            BuffDef buffDef = ScriptableObject.CreateInstance<BuffDef>();
            buffDef.buffColor = new Color(0.5f, 0.5f, 1f);
            buffDef.canStack = false;
            buffDef.eliteDef = null;
            buffDef.iconSprite = Addressables.LoadAssetAsync<BuffDef>("RoR2/DLC1/StrengthenBurn/bdStrongerBurn.asset").WaitForCompletion().iconSprite;
            buffDef.isDebuff = true;
            buffDef.name = "RailgunnerFrostfireBuff";
            FrostfireBuff = buffDef;
            ContentAddition.AddBuffDef(buffDef);

            DotController.DotDef dotDef = new DotController.DotDef();
            dotDef.associatedBuff = FrostfireBuff;
            dotDef.damageCoefficient = 2f;
            dotDef.damageColorIndex = DamageColorIndex.Item;
            dotDef.terminalTimedBuff = FrostfireBuff;
            dotDef.interval = 0.5f;
            FrostfireIndex = DotAPI.RegisterDotDef(dotDef);
        }

        private static void HealthComponent_TakeDamage(On.RoR2.HealthComponent.orig_TakeDamage orig, HealthComponent self, DamageInfo di)
        {
            if (self && self.body && di != null && di.inflictor && di.inflictor.TryGetComponent(out CharacterBody body) && cryoActiveBodies.Contains(body))
            {
                int level = SkillUpgrade.GetLevel(body, SkillName);
                if (level > 0)
                {
                    self.body.AddTimedBuff(FrostfireBuff, 2f * level);
                    int ignitionTanks = body.inventory ? body.inventory.GetItemCount(DLC1Content.Items.StrengthenBurn) : 0;
                    DotController.InflictDot(self.gameObject, di.attacker, FrostfireIndex, 2f * level, (1 + 3 * ignitionTanks), 1);
                }
            }
            orig(self, di);
        }

        public override void SetupSkill()
        {
            RegisterFrostfireBuff();
            On.RoR2.HealthComponent.TakeDamage += HealthComponent_TakeDamage;
            RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPIOnGetStatCoefficients;
            base.SetupSkill();
        }

        // Previously only ever applied to player 1.
        private static void RecalculateStatsAPIOnGetStatCoefficients(CharacterBody sender, StatHookEventArgs args)
        {
            int level = SkillUpgrade.GetLevel(sender, SkillName);
            if (level > 0)
            {
                args.specialCooldownMultAdd -= 1 - (10f / (level + 10f));
            }
        }
    }
}
