using System;
using System.Collections.Generic;
using RoR2;
using RoR2.Skills;
using RoR2.UI;
using SkillsPlusPlus.Generic;
using UnityEngine;

namespace SkillsPlusPlus.UI
{
    /// <summary>
    /// Adds an "Upgrade" section to skill tooltips: what one level gives, the player's current level (on the HUD),
    /// and only those generic bonuses the skill actually gets (no cooldown text for skills without a cooldown, no
    /// extra charges for single-charge skills). Refreshed every time a skill tooltip is shown.
    /// </summary>
    [RequireComponent(typeof(TooltipController))]
    internal class SkillsPlusPlusTooltipController : MonoBehaviour
    {
        private TooltipController tooltipController;
        private GameObject upgradeRect;
        private TMPro.TextMeshProUGUI upgradeLabel;

        // Kept for compatibility with code that sets the token directly.
        public string skillUpgradeToken;

        private void Awake()
        {
            tooltipController = GetComponent<TooltipController>();
        }

        private void EnsureRect()
        {
            if (upgradeRect || !tooltipController || !tooltipController.bodyLabel) return;
            var panelTransform = tooltipController.tooltipFlipTransform;
            GameObject bodyRectGameObject = tooltipController.bodyLabel.transform.parent.gameObject;
            upgradeRect = Instantiate(bodyRectGameObject, panelTransform);
            upgradeRect.transform.SetSiblingIndex(Math.Max(0, panelTransform.childCount - 2));
            upgradeLabel = upgradeRect.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        }

        internal void Show(SkillUpgradeTooltipProvider provider)
        {
            string skillName = provider.GetSkillName();
            skillUpgradeToken = SkillUpgradeTooltipProvider.SkillNameToToken(skillName);
            EnsureRect();
            if (!upgradeRect) return;

            string description = Describe(skillName, provider.GetSkillDef(), provider.GetSlot());
            upgradeRect.SetActive(description != null);
            if (description == null || !upgradeLabel) return;

            int level = provider.GetLevel();
            int maxLevel = provider.GetMaxLevel();
            string levelText = maxLevel > 0 ? $"{level}/{maxLevel}" : level.ToString();
            upgradeLabel.text = level > 0
                ? Language.GetStringFormatted("TOOLTIP_UPGRADE_LEVEL_DESCRIPTION", description, levelText)
                : Language.GetStringFormatted("TOOLTIP_UPGRADE_DESCRIPTION", description);
        }

        /// <summary>Hides the upgrade section (the tooltip is reused for items, equipment, etc.).</summary>
        internal void Hide()
        {
            if (upgradeRect) upgradeRect.SetActive(false);
        }

        private static string Describe(string skillName, SkillDef def, SkillSlot slot)
        {
            if (string.IsNullOrEmpty(skillName)) return null;
            var parts = new List<string>();

            string token = SkillUpgradeTooltipProvider.SkillNameToToken(skillName);
            if (SlotUpgrades.IsGeneric(skillName))
            {
                // Passives (loadout "Misc" row, e.g. Acrid's Poison/Blight) have no HUD skill icon and cannot be bought.
                if (slot == SkillSlot.None) return null;
                int damage = Mathf.RoundToInt(SlotUpgrades.DefaultDamagePerLevel(slot) * 100f);
                if (damage > 0) parts.Add(Language.GetStringFormatted("TOOLTIP_UPGRADE_GENERIC_DAMAGE", damage));
            }
            else if (!Language.IsTokenInvalid(token))
            {
                parts.Add(Language.GetString(token));
            }
            else
            {
                return null;
            }

            if (SlotUpgrades.GetsGenericCooldown(def))
                parts.Add(Language.GetStringFormatted("TOOLTIP_UPGRADE_GENERIC_COOLDOWN", Mathf.RoundToInt(SlotUpgrades.GenericCooldown.Value * 100f)));
            if (SlotUpgrades.GetsGenericStock(def))
                parts.Add(Language.GetStringFormatted("TOOLTIP_UPGRADE_GENERIC_STOCK", SlotUpgrades.GenericLevelsPerStock.Value));

            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }
    }
}
