using UnityEngine;

using SkillsPlusPlus.Modifiers;
using RoR2;

using EntityStates;
using EntityStates.VoidSurvivor.Weapon;
using static EntityStates.VoidSurvivor.VoidBlinkBase;

using RoR2.Projectile;
using RoR2.Skills;
using UnityEngine.AddressableAssets;
using R2API;
using GlobalEventManager = On.RoR2.GlobalEventManager;
using UnityEngine.Networking;

namespace SkillsPlusPlus.Source.Modifiers
{
    internal class VoidFiendSkillModifiers
    {
    }


    //[SkillLevelModifier("PlaceSpiderMine", typeof(ChargeCrabCannon), typeof(ChargeCorruptHandBeam), typeof(ChargeCrushBase), typeof(ChargeCrushHealth), typeof(CrushBase), typeof(CrushHealth), typeof(EnterSwingMelee), typeof(FireBlaster1), typeof(FireBlaster2), typeof(FireBlaster3), typeof(FireBlaster4),
    //    typeof(FireBlasterBase), typeof(FireCorruptDisks), typeof(FireCorruptHandBeam), typeof(FireCrabCannon), typeof(FireRepulsion), typeof(FireTwinBlaster), typeof(ReadyMegaBlaster), typeof(Suppress)
    //    , typeof(SwingMelee1), typeof(SwingMelee2), typeof(SwingMelee3), typeof(SwingMeleeBase))]
    //class VoidFiendSkillModifier : BaseSkillModifier {

    //    public override void OnSkillEnter(BaseState skillState, int level) {
    //        Logger.Debug("Skill entered: " + skillState.ToString());
    //    }

    //}
    [SkillLevelModifier(new[] { "FireHandBeam", "ChargeHandBeam", "FireCorruptBeam", "ChargeCorruptHandBeam" }, typeof(FireHandBeam), typeof(ChargeHandBeam), typeof(FireCorruptHandBeam), typeof(ChargeCorruptHandBeam))]
    class VoidFiendHandBeamSkillModifier : BaseSkillModifier {
        private static readonly string[] beamSkills = { "FireHandBeam", "ChargeHandBeam", "FireCorruptBeam", "ChargeCorruptHandBeam" };
        public BuffDef VoidFiendSpeedBuff;

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            Logger.Debug($"OnSkillEnter: {skillState.GetType().Name}");
            if(skillState is FireHandBeam beam) {
                beam.damageCoefficient = MultScaling(beam.damageCoefficient, 0.10f, level);
                //beam.bulletCount = MultScaling(beam.bulletCount, 1, level);
            } else if (skillState is FireCorruptHandBeam corruptbeam) {
                corruptbeam.beamVfxPrefab.transform.localScale = new Vector3(corruptbeam.beamVfxPrefab.transform.localScale.x, corruptbeam.beamVfxPrefab.transform.localScale.y, MultScaling(1, 0.30f, level)); //vfx should extend a bit farther imo 
                Logger.Debug("FireCorruptHandBeam" + corruptbeam.maxDistance);
                corruptbeam.maxDistance = MultScaling(corruptbeam.maxDistance, 0.25f, level);
                corruptbeam.minDistance = MultScaling(corruptbeam.minDistance, 0.25f, level);
                Logger.Debug("FireCorruptHandBeam" + corruptbeam.maxDistance);
                Logger.Debug("firecorruptmanged " + corruptbeam.damageCoefficientPerSecond);
                corruptbeam.damageCoefficientPerSecond = MultScaling(corruptbeam.damageCoefficientPerSecond, 0.10f, level);
                Logger.Debug("firecorruptmanged " + corruptbeam.damageCoefficientPerSecond);
                // Buffs live on the host, which also runs this state for every Void Fiend.
                CharacterBody body = corruptbeam.characterBody;
                if (NetworkServer.active && body && VoidFiendSpeedBuff && level > 0 && !body.HasBuff(VoidFiendSpeedBuff))
                {
                    body.AddBuff(VoidFiendSpeedBuff);
                }
            }
        }
        public override void OnSkillExit(BaseState skillState, int level)
        {
            base.OnSkillExit(skillState, level);
            if (skillState is FireCorruptHandBeam corruptbeam)
            {
                CharacterBody body = corruptbeam.characterBody;
                if (NetworkServer.active && body && VoidFiendSpeedBuff && body.HasBuff(VoidFiendSpeedBuff))
                {
                    body.RemoveBuff(VoidFiendSpeedBuff);
                }
            }
        }

        public override void SetupSkill()
        {
            RegisterSpeedBuff();
            On.RoR2.GlobalEventManager.OnHitEnemy += GlobalEventManagerOnOnHitEnemy;
            RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPIOnGetStatCoefficients;
            base.SetupSkill();
        }

        private void RecalculateStatsAPIOnGetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (sender.HasBuff(VoidFiendSpeedBuff))
            {
                args.baseMoveSpeedAdd += MultScaling(0.2f, 0.10f, SkillUpgrade.GetLevel(sender, beamSkills));
            }
        }

        public void RegisterSpeedBuff()
        {
            BuffDef buffDef = ScriptableObject.CreateInstance<BuffDef>();

            buffDef.buffColor = new Color(0.9f, 0.6f, 0.9f);
            buffDef.canStack = false;
            buffDef.eliteDef = null;
            buffDef.iconSprite = Addressables.LoadAssetAsync<BuffDef>("RoR2/Base/SprintOutOfCombat/bdWhipBoost.asset").WaitForCompletion().iconSprite;
            buffDef.isDebuff = false;
            buffDef.name = "VoidFiendSpeedBuff";

            VoidFiendSpeedBuff = buffDef;
            ContentAddition.AddBuffDef(buffDef);
        }

        private void GlobalEventManagerOnOnHitEnemy(GlobalEventManager.orig_OnHitEnemy orig, RoR2.GlobalEventManager self, DamageInfo damageinfo, GameObject victim)
        {
            orig(self, damageinfo, victim);
            if (damageinfo == null || !damageinfo.attacker || !victim) return;
            if (damageinfo.damageType.damageSource != DamageSource.Primary) return;
            if (!damageinfo.attacker.TryGetComponent(out CharacterBody attacker)) return;
            int debuffTimerAdd = SkillUpgrade.GetLevel(attacker, "FireHandBeam", "ChargeHandBeam");
            if (debuffTimerAdd == 0) return;
            if (attacker.TryGetComponent(out VoidSurvivorController controller))
            {
                if (controller.isCorrupted)
                    return;
            }
            Logger.Debug(debuffTimerAdd + " debuffing longer");
            if (victim.TryGetComponent(out CharacterBody charbody))
            {
                charbody.AddTimedBuff(RoR2Content.Buffs.Slow50, 3 + debuffTimerAdd); // 3 is the one the damagetype adds 
            }
        }
    }

    [SkillLevelModifier("ChargeMegaBlaster", typeof(ChargeMegaBlaster), typeof(FireMegaBlasterBase), // flood
        typeof(FireMegaBlasterBig), typeof(FireMegaBlasterSmall))]
    internal class VoidFiendChargeMegaSkillModifier : BaseSkillModifier
    {


        public override void OnSkillEnter(BaseState skillState, int level)
        {
            if (skillState is ChargeMegaBlaster chargeMegaBlaster)
            {
                Logger.Debug("ChargeMegaBlaster");
                chargeMegaBlaster.baseDuration = MultScaling(2f, -0.15f, level);
            }
            else if (skillState is FireMegaBlasterBase firemegablaster)
            {
                Logger.Debug("FireMegaBlasterBase");
                firemegablaster.projectilePrefab.transform.localScale = new Vector3(MultScaling(1, 0.15f, level), MultScaling(1, 0.15f, level), MultScaling(1, 0.15f, level));
                Logger.Debug(firemegablaster.projectilePrefab.tag);
                if (!firemegablaster.projectilePrefab.TryGetComponent(out ProjectileImpactExplosion impactExplosion))
                    return;
                impactExplosion.blastRadius = MultScaling(5, 0.15f, level);
                impactExplosion.blastDamageCoefficient = MultScaling(1, 0.10f, level);
                impactExplosion.impactEffect.transform.localScale = new Vector3(MultScaling(1, 0.15f, level), MultScaling(1, 0.15f, level), MultScaling(1, 0.15f, level));
            } /*else if (skillState is FireMegaBlasterBig) {
                Logger.Debug("FireMegaBlasterBig");
            } else if (skillState is FireMegaBlasterSmall) {
                Logger.Debug("FireMegaBlasterSmall");
            }*/
        }

    }

    [SkillLevelModifier("FireCorruptDisk", typeof(FireCorruptDisks))] // corrupt flood
    internal class VoidFiendFireCorruptDiskSkillModifier : SimpleSkillModifier<FireCorruptDisks>
    {
        public override void OnSkillEnter(FireCorruptDisks skillState, int level)
        {
            if (!(skillState is FireCorruptDisks fireCorruptDisks)) return;
            Logger.Debug("FireCorruptDisks");
            fireCorruptDisks.projectilePrefab.transform.localScale = new Vector3(MultScaling(1, 0.10f, level), MultScaling(1, 0.10f, level), MultScaling(1, 0.10f, level));
            Logger.Debug(fireCorruptDisks.projectilePrefab.tag);
            if (!fireCorruptDisks.projectilePrefab.TryGetComponent(out ProjectileImpactExplosion impactExplosion))
                return;
            impactExplosion.blastDamageCoefficient = MultScaling(1, 0.15f, level);
        }

        public override void SetupSkill()
        {
            //On.RoR2.GlobalEventManager.OnHitEnemy += GlobalEventManagerOnOnHitEnemy;
            RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPIOnGetStatCoefficients;
            base.SetupSkill();
        }

        private void RecalculateStatsAPIOnGetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            int skilllevel = SkillUpgrade.GetLevel(sender, "FireCorruptDisk");
            if (skilllevel <= 0) return;
            if (sender.TryGetComponent(out VoidSurvivorController controller) && controller.isCorrupted)
            {
                args.secondaryCooldownMultAdd -= 1 - MultScaling(1, -0.10f, skilllevel);
            }
        }
    }

    [SkillLevelModifier("VoidBlinkUp", typeof(VoidBlinkUp))]
    internal class VoidFiendVoidBlinkUpSkillModifier : SimpleSkillModifier<VoidBlinkUp>
    {
        private BuffDef VoidFiendArmorBuff;
        public override void OnSkillExit(VoidBlinkUp skillState, int level)
        {
            if (NetworkServer.active && level > 0 && skillState.characterBody)
                skillState.characterBody.AddTimedBuff(VoidFiendArmorBuff, 3);
        }
        public override void SetupSkill()
        {
            RegisterArmorBuff();
            RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPIOnGetStatCoefficients;
            base.SetupSkill();
        }

        public void RegisterArmorBuff()
        {
            BuffDef buffDef = ScriptableObject.CreateInstance<BuffDef>();

            buffDef.buffColor = new Color(0.9f, 0.4f, 0.7f);
            buffDef.canStack = false;
            buffDef.eliteDef = null;
            buffDef.iconSprite = Addressables.LoadAssetAsync<BuffDef>("RoR2/Base/Common/bdHiddenInvincibility.asset").WaitForCompletion().iconSprite;
            buffDef.isDebuff = false;
            buffDef.name = "VoidFiendDamageSpeedBuff";

            VoidFiendArmorBuff = buffDef;
            ContentAddition.AddBuffDef(buffDef);
        }
        private void RecalculateStatsAPIOnGetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (sender.HasBuff(VoidFiendArmorBuff))
            {
                args.armorAdd += AdditiveScaling(0, 50, SkillUpgrade.GetLevel(sender, "VoidBlinkUp"));
            }
        }
    }

    [SkillLevelModifier("VoidBlinkDown", typeof(VoidBlinkDown))]
    internal class VoidFiendVoidBlinkDownSkillModifier : SimpleSkillModifier<VoidBlinkDown>
    {
        private BuffDef VoidFiendDamageSpeedBuff;
        public override void OnSkillExit(VoidBlinkDown skillState, int level)
        {
            if (NetworkServer.active && level > 0 && skillState.characterBody)
                skillState.characterBody.AddTimedBuff(VoidFiendDamageSpeedBuff, level + 1);
        }

        public override void SetupSkill()
        {
            RegisterDamageSpeedBuff();
            RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPIOnGetStatCoefficients;
            base.SetupSkill();
        }

        private void RecalculateStatsAPIOnGetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender.HasBuff(VoidFiendDamageSpeedBuff)) return;
            args.attackSpeedMultAdd += 0.20f;
            args.moveSpeedMultAdd += 0.20f;
        }

        public void RegisterDamageSpeedBuff()
        {
            BuffDef buffDef = ScriptableObject.CreateInstance<BuffDef>();

            buffDef.buffColor = new Color(0.9f, 0.4f, 0.7f);
            buffDef.canStack = false;
            buffDef.eliteDef = null;
            buffDef.iconSprite = Addressables.LoadAssetAsync<BuffDef>("RoR2/Base/SprintOutOfCombat/bdWhipBoost.asset").WaitForCompletion().iconSprite;
            buffDef.isDebuff = false;
            buffDef.name = "VoidFiendDamageSpeedBuff";

            VoidFiendDamageSpeedBuff = buffDef;
            ContentAddition.AddBuffDef(buffDef);
        }
    }

    [SkillLevelModifier("CrushCorruption", typeof(CrushCorruption), typeof(ChargeCrushCorruption))]
    internal class VoidFiendCrushCorruptionSkillModifier : BaseSkillModifier
    {

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            if (skillState is CrushCorruption crushCorruption)
            {
                Logger.Debug("CrushCorruption");
                crushCorruption.selfHealFraction = AdditiveScaling(crushCorruption.selfHealFraction, 0.15f, level);
            }
        }
    }

    [SkillLevelModifier("CrushHealth", typeof(CrushHealth), typeof(ChargeCrushHealth))]
    internal class VoidFiendCrushHealthSkillModifier : BaseSkillModifier
    {
        public override void OnSkillEnter(BaseState skillState, int level)
        {
            if (skillState is CrushHealth crushHealth)
            {
                Logger.Debug("CrushHealth");
                crushHealth.corruptionChange = AdditiveScaling(crushHealth.corruptionChange, 15f, level);
            }
            else if (skillState is ChargeCrushHealth)
            {
                Logger.Debug("ChargeCrushHealth");
            }
        }
    }
}
