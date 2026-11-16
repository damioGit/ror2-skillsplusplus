using RoR2;
using RoR2.Skills;
using RoR2.UI;
using UnityEngine;

namespace SkillsPlusPlus.UI
{
    /// <summary>Attached to skill icons (HUD and loadout menu); tells the tooltip which skill it is about.</summary>
    internal class SkillUpgradeTooltipProvider : MonoBehaviour
    {
        public string skillName;
        public SkillDef skillDef;
        public SkillSlot slot = SkillSlot.None;
        public SkillIcon skillIcon;

        private void Awake()
        {
            skillIcon = GetComponent<SkillIcon>();
        }

        internal static string SkillNameToToken(string skillName)
        {
            if (skillName == null) return null;
            return (skillName + "_UPGRADE_DESCRIPTION").ToUpperInvariant();
        }

        /// <summary>The skill this icon shows: fixed for loadout buttons, the current skill for HUD icons.</summary>
        internal SkillDef GetSkillDef()
        {
            if (skillIcon && skillIcon.targetSkill) return skillIcon.targetSkill.skillDef;
            return skillDef;
        }

        internal string GetSkillName()
        {
            if (skillIcon && skillIcon.targetSkill) return SkillUpgrade.SkillName(skillIcon.targetSkill.skillDef);
            return skillName ?? SkillUpgrade.SkillName(skillDef);
        }

        internal SkillSlot GetSlot()
        {
            if (skillIcon) return skillIcon.targetSkillSlot;
            return slot;
        }

        /// <summary>The local player's level in this skill (HUD only), or -1 when unknown.</summary>
        internal int GetLevel()
        {
            if (!skillIcon || !skillIcon.targetSkill) return -1;
            return SkillUpgrade.GetLevel(skillIcon.targetSkill.characterBody, GetSkillName());
        }

        internal string GetToken() => SkillNameToToken(GetSkillName());
    }
}
