using System;
using RoR2;
using RoR2.UI;
using SkillsPlusPlus.Generic;
using UnityEngine;

namespace SkillsPlusPlus.UI
{
    /// <summary>
    /// Adds an "Upgrade" section to skill tooltips: what one level gives, the player's current level (on the HUD),
    /// and the generic bonuses for skills that use them. Refreshed every time a skill tooltip is shown, since the
    /// tooltip object is reused between skills.
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

        /// <summary>Shows the upgrade text for <paramref name="skillName"/> (level -1 = unknown, e.g. in the loadout menu).</summary>
        internal void Show(string skillName, int level)
        {
            skillUpgradeToken = SkillUpgradeTooltipProvider.SkillNameToToken(skillName);
            EnsureRect();
            if (!upgradeRect) return;

            string description = Describe(skillName);
            upgradeRect.SetActive(description != null);
            if (description == null || !upgradeLabel) return;

            upgradeLabel.text = level > 0
                ? Language.GetStringFormatted("TOOLTIP_UPGRADE_LEVEL_DESCRIPTION", description, level)
                : Language.GetStringFormatted("TOOLTIP_UPGRADE_DESCRIPTION", description);
        }

        /// <summary>Hides the upgrade section (the tooltip is reused for items, equipment, etc.).</summary>
        internal void Hide()
        {
            if (upgradeRect) upgradeRect.SetActive(false);
        }

        private static string Describe(string skillName)
        {
            if (string.IsNullOrEmpty(skillName)) return null;
            string token = SkillUpgradeTooltipProvider.SkillNameToToken(skillName);
            bool hasText = !Language.IsTokenInvalid(token);
            int cooldown = Mathf.RoundToInt(SlotUpgrades.GenericCooldown.Value * 100f);
            int every = SlotUpgrades.GenericLevelsPerStock.Value;

            if (SlotUpgrades.IsGeneric(skillName))
            {
                int damage = Mathf.RoundToInt(SlotUpgrades.SecondaryUtilityDamage.Value * 100f);
                return Language.GetStringFormatted("TOOLTIP_UPGRADE_GENERIC", damage, cooldown, every);
            }
            if (!hasText) return null;

            string text = Language.GetString(token);
            var modifier = SkillModifierManager.GetSkillModifierByName(skillName);
            if (modifier != null && modifier.UseGenericCooldownAndStock && SlotUpgrades.GenericEnabled.Value)
            {
                text += ", " + Language.GetStringFormatted("TOOLTIP_UPGRADE_GENERIC_EXTRA", cooldown, every);
            }
            return text;
        }
    }
}
