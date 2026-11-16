using EntityStates;
using EntityStates.Seeker;
using RoR2;
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
            skillState.dmgBuffIncrease += 0.05f * level;
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

    /// <summary>Unseen Hand: longer targeting range.</summary>
    [SkillLevelModifier("SeekerBodyUnseenHand", typeof(UnseenHand))]
    internal class SeekerUnseenHandSkillModifier : SimpleSkillModifier<UnseenHand>
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.10f;

        public override void OnSkillEnter(UnseenHand skillState, int level)
        {
            UnseenHand.maxDistance = MultScaling(Originals.Get("UnseenHand.maxDistance", UnseenHand.maxDistance), 0.20f, level);
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
                manager.soulSpiralLimit = 6 + level;
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
            skillState.duration += 1f * level;
            skillState.timePenalty = Mathf.Max(0.2f, skillState.timePenalty - 0.2f * level);
            skillState.failureCooldownPenalty = Mathf.Max(0f, skillState.failureCooldownPenalty - 1f * level);
        }
    }

    /// <summary>Palm Blast: charges faster. (Pierce is already infinite, so no pierce upgrade.)</summary>
    [SkillLevelModifier("SeekerPalmBlast", typeof(BaseChargePalmBlast), typeof(BaseFirePalmBlast))]
    internal class SeekerPalmBlastSkillModifier : BaseSkillModifier
    {
        public override float SlotDamageBonusPerLevel(SkillSlot slot) => 0.20f;

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            if (skillState is BaseChargePalmBlast charge)
            {
                charge.baseChargeDuration = MultScaling(charge.baseChargeDuration, -0.15f, level);
            }
        }
    }
}
