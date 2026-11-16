using EntityStates;
using EntityStates.Seeker;
using RoR2;
using RoR2.Projectile;
using SkillsPlusPlus.Util;
using UnityEngine;

// Seeker (Seekers of the Storm). Upgrades target what actually limits each skill in 1.5.0:
// Meditate's QTE window/penalties, Sojourn's health cost, Soul Spiral's orb cap, Palm Blast's charge time.
// Damage comes from the host-side slot bonus.
namespace SkillsPlusPlus.Modifiers
{
    /// <summary>Spirit Punch: bigger damage bonus per Tranquility stack (vanilla +20% per stack, +5 pts per level).</summary>
    [SkillLevelModifier("SeekerBodySpiritPunchCrosshair", typeof(SpiritPunch))]
    internal class SeekerSpiritPunchSkillModifier : SimpleSkillModifier<SpiritPunch>
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.06f;

        public override void OnSkillEnter(SpiritPunch skillState, int level)
        {
            skillState.dmgBuffIncrease += 0.03f * level;
        }
    }

    /// <summary>Soul Search: bigger explosions.</summary>
    [SkillLevelModifier("SeekerBodySoulSearch", typeof(SoulSearch))]
    internal class SeekerSoulSearchSkillModifier : SimpleSkillModifier<SoulSearch>
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.10f;

        public override void OnSkillEnter(SoulSearch skillState, int level)
        {
            skillState.blastRadius = MultScaling(skillState.blastRadius, 0.15f, level);
        }
    }

    /// <summary>Unseen Hand: longer targeting range, and heals +1.5% of damage dealt per level (vanilla 3.5%).</summary>
    [SkillLevelModifier("SeekerBodyUnseenHand", typeof(UnseenHand))]
    internal class SeekerUnseenHandSkillModifier : SimpleSkillModifier<UnseenHand>
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.10f;

        public override void OnSkillEnter(UnseenHand skillState, int level)
        {
            UnseenHand.maxDistance = MultScaling(Originals.Get("UnseenHand.maxDistance", UnseenHand.maxDistance), 0.20f, level);
            ProjectileTweaks.Register(UnseenHand.fistProjectilePrefab, (projectile, owner, lvl) =>
            {
                if (projectile.TryGetComponent(out UnseenHandHealingProjectile healing))
                {
                    healing.fractionOfDamage += 0.015f * lvl;
                }
            }, "SeekerBodyUnseenHand");
        }
    }

    /// <summary>Soul Spiral: +1 orb to the cap (vanilla 6) per level.</summary>
    [SkillLevelModifier("SeekerBodySoulSpiral", typeof(SoulSpiral))]
    internal class SeekerSoulSpiralSkillModifier : SimpleSkillModifier<SoulSpiral>
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.10f;

        public override void OnSkillEnter(SoulSpiral skillState, int level)
        {
            if (skillState.characterBody && skillState.characterBody.TryGetComponent(out SeekerSoulSpiralManager manager))
            {
                manager.soulSpiralLimit = 6 + Mathf.Min(level, 4);
            }
        }
    }

    /// <summary>Sojourn: costs less health per second of flight and explodes bigger.</summary>
    [SkillLevelModifier("SeekerBodySojourn", typeof(Sojourn))]
    internal class SeekerSojournSkillModifier : SimpleSkillModifier<Sojourn>
    {
        private const string SkillName = "SeekerBodySojourn";

        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.12f;

        public override void SetupSkill()
        {
            base.SetupSkill();
            On.RoR2.SojournVehicle.OnPassengerEnter += (orig, self, passenger) =>
            {
                int level = passenger ? SkillUpgrade.GetLevel(passenger.GetComponent<CharacterBody>(), SkillName) : 0;
                if (level > 0)
                {
                    // Vanilla: 7.5% of total health per tick, -1 point per level, never below 3.5%.
                    self.incrementalFlightDamage = Mathf.Max(0.035f, self.incrementalFlightDamage - 0.01f * level);
                    self.baseMaxBlastSize *= 1f + 0.12f * level;
                }
                orig(self, passenger);
            };
        }
    }

    /// <summary>Reprieve: more barrier for you and for allies you fly through.</summary>
    [SkillLevelModifier("SeekerBodyReprieve", typeof(Reprieve))]
    internal class SeekerReprieveSkillModifier : SimpleSkillModifier<Reprieve>
    {
        private const string SkillName = "SeekerBodyReprieve";

        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.10f;

        public override void OnSkillEnter(Reprieve skillState, int level)
        {
            skillState.barrierOnReprieveEnter += 0.08f * level;
        }

        public override void SetupSkill()
        {
            base.SetupSkill();
            On.RoR2.ReprieveVehicle.OnPassengerEnter += (orig, self, passenger) =>
            {
                int level = passenger ? SkillUpgrade.GetLevel(passenger.GetComponent<CharacterBody>(), SkillName) : 0;
                if (level > 0)
                {
                    self.overlapBarrierPercentAlly += 0.03f * level;
                }
                orig(self, passenger);
            };
        }
    }

    /// <summary>
    /// Meditate: the real limit is the 5-arrow QTE. +1 s to enter it per level, smaller penalty per wrong input
    /// and a shorter cooldown penalty when you fail.
    /// </summary>
    [SkillLevelModifier("SeekerBodyMeditate", typeof(Meditate))]
    internal class SeekerMeditateSkillModifier : SimpleSkillModifier<Meditate>
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.12f;

        public override void OnSkillEnter(Meditate skillState, int level)
        {
            skillState.duration += Mathf.Min(4f, 1f * level);
            skillState.timePenalty = Mathf.Max(0.2f, skillState.timePenalty - 0.2f * level);
            skillState.failureCooldownPenalty = Mathf.Max(0f, skillState.failureCooldownPenalty - 1f * level);
        }

        // From level 3 the first wrong arrow of each meditation is forgiven: no time penalty, still a perfect run.
        private const int ForgiveLevel = 3;
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Meditate, object> forgiven =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Meditate, object>();

        public override void SetupSkill()
        {
            base.SetupSkill();
            On.EntityStates.Seeker.Meditate.Update += (orig, self) =>
            {
                bool wrongInput = false;
                bool wasMissed = self.missedInput;
                float ageBefore = self.fixedAge;
                if (self.isAuthority && self.inputBank && self.seekerController && !self.meditationWrapUp
                    && !forgiven.TryGetValue(self, out _)
                    && SkillUpgrade.GetLevel(self.characterBody, "SeekerBodyMeditate") >= ForgiveLevel)
                {
                    int pressed = -1;
                    if (self.inputBank.rawMoveUp.justPressed) pressed = 0;
                    if (self.inputBank.rawMoveDown.justPressed) pressed = 1;
                    if (self.inputBank.rawMoveRight.justPressed) pressed = 2;
                    if (self.inputBank.rawMoveLeft.justPressed) pressed = 3;
                    wrongInput = pressed != -1 && self.seekerController.meditationInputStep < 5 && pressed != self.seekerController.GetNextMeditationStep();
                }
                orig(self);
                if (wrongInput && self.missedInput && !wasMissed)
                {
                    forgiven.Add(self, null);
                    self.missedInput = false;
                    self.fixedAge = Mathf.Max(ageBefore, self.fixedAge - self.timePenalty);
                }
            };
        }
    }

    /// <summary>Palm Blast: charges faster. (Pierce is already infinite, so no pierce upgrade.)</summary>
    [SkillLevelModifier("SeekerPalmBlast", typeof(BaseChargePalmBlast), typeof(BaseFirePalmBlast))]
    internal class SeekerPalmBlastSkillModifier : BaseSkillModifier
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.15f;

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            if (skillState is BaseChargePalmBlast charge)
            {
                charge.baseChargeDuration = MultScaling(charge.baseChargeDuration, -0.15f, level);
            }
            else if (skillState is BaseFirePalmBlast fire)
            {
                // Bigger hand (15% per level), applied to each spawned projectile on the host.
                ProjectileTweaks.Register(fire.chargedProjectilePrefab, (projectile, owner, lvl) => projectile.transform.localScale *= 1f + 0.15f * lvl, "SeekerPalmBlast");
                ProjectileTweaks.Register(fire.projectilePrefab, (projectile, owner, lvl) => projectile.transform.localScale *= 1f + 0.15f * lvl, "SeekerPalmBlast");
            }
        }

        public override void SetupSkill()
        {
            base.SetupSkill();
            // Vanilla gives Tranquility every 3 characters hit. From level 2 it takes only 2.
            On.PalmBlastProjectileController.IncreaseCharactersHit += (orig, self, isSpecialEnemy) =>
            {
                if (!isSpecialEnemy && self.charactersHit == 1 && SkillUpgrade.GetLevel(self.ownerBody, "SeekerPalmBlast") >= 2)
                {
                    self.charactersHit = 2;
                }
                return orig(self, isSpecialEnemy);
            };
        }
    }
}
