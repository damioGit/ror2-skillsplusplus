using System;
using BepInEx.Configuration;
using R2API;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using RoR2;
using SkillsPlusPlus.Modifiers;
using UnityEngine;
using UnityEngine.Networking;

namespace SkillsPlusPlus.Generic
{
    /// <summary>
    /// Upgrades that work for every skill, based only on which slot it is in and its level:
    ///
    /// - Damage: on the host, damage tagged with a skill slot (DamageSource.Primary/Secondary/Utility/Special) is
    ///   multiplied by the attacker's level in that slot. Skills with a dedicated modifier opt in through
    ///   <see cref="BaseSkillModifier.SlotDamageBonusPerLevel"/>; skills without one (e.g. survivors from other mods)
    ///   get the default bonus for their slot.
    /// - Cooldown and stock: only for skills without a dedicated modifier, so any survivor from any mod still gets
    ///   something out of skill points.
    ///
    /// Everything is read per player from synced levels, so it is correct in multiplayer.
    /// </summary>
    internal static class SlotUpgrades
    {
        internal static ConfigEntry<bool> GenericEnabled;
        internal static ConfigEntry<float> PrimaryDamage;
        internal static ConfigEntry<float> SecondaryUtilityDamage;
        internal static ConfigEntry<float> SpecialDamage;
        internal static ConfigEntry<float> GenericCooldown;
        internal static ConfigEntry<int> GenericLevelsPerStock;

        internal static void Init(ConfigFile config)
        {
            GenericEnabled = config.Bind("Generic upgrades", "Upgrade any skill", true,
                "Lets you spend points on skills that have no dedicated Skills++ upgrade (e.g. survivors from other mods). They get damage, cooldown and stock bonuses.");
            PrimaryDamage = config.Bind("Generic upgrades", "Primary damage per level", 0.06f,
                "Damage multiplier per level for primary skills (0.06 = +6%, compounding).");
            SecondaryUtilityDamage = config.Bind("Generic upgrades", "Secondary/utility damage per level", 0.10f,
                "Damage multiplier per level for secondary and utility skills.");
            SpecialDamage = config.Bind("Generic upgrades", "Special damage per level", 0.12f,
                "Damage multiplier per level for special skills.");
            GenericCooldown = config.Bind("Generic upgrades", "Cooldown reduction per level", 0.06f,
                "Cooldown reduction per level for skills without a dedicated upgrade (compounding).");
            GenericLevelsPerStock = config.Bind("Generic upgrades", "Levels per extra charge", 3,
                "Skills without a dedicated upgrade gain one extra charge every this many levels. 0 disables.");

            ModSettingsManager.AddOption(new CheckBoxOption(GenericEnabled));
            ModSettingsManager.AddOption(new StepSliderOption(PrimaryDamage, new StepSliderConfig { min = 0f, max = 0.3f, increment = 0.01f }));
            ModSettingsManager.AddOption(new StepSliderOption(SecondaryUtilityDamage, new StepSliderConfig { min = 0f, max = 0.3f, increment = 0.01f }));
            ModSettingsManager.AddOption(new StepSliderOption(SpecialDamage, new StepSliderConfig { min = 0f, max = 0.3f, increment = 0.01f }));
            ModSettingsManager.AddOption(new StepSliderOption(GenericCooldown, new StepSliderConfig { min = 0f, max = 0.2f, increment = 0.01f }));
            ModSettingsManager.AddOption(new IntSliderOption(GenericLevelsPerStock, new IntSliderConfig { min = 0, max = 10 }));

            On.RoR2.HealthComponent.TakeDamageProcess += TakeDamageProcess;
            RecalculateStatsAPI.GetStatCoefficients += GetStatCoefficients;
            On.RoR2.CharacterBody.RecalculateStats += RecalculateStats;
            CharacterBody.onBodyStartGlobal += TrackSkillActivations;
        }

        #region Fallback for mods that don't tag damage with a skill slot

        // Some modded survivors (e.g. Seamstress, parts of Starstorm 2) leave DamageSource empty. For those we
        // attribute untagged, non-proc damage to the skill the player activated most recently.
        private const float AttributionWindow = 1.5f;

        private sealed class LastSkill { public SkillSlot slot; public float time; }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<CharacterBody, LastSkill> lastSkill =
            new System.Runtime.CompilerServices.ConditionalWeakTable<CharacterBody, LastSkill>();

        private static void TrackSkillActivations(CharacterBody body)
        {
            if (!NetworkServer.active || !body || !body.isPlayerControlled) return;
            body.onSkillActivatedServer += skill =>
            {
                if (!body || !body.skillLocator) return;
                var entry = lastSkill.GetOrCreateValue(body);
                entry.slot = body.skillLocator.FindSkillSlot(skill);
                entry.time = Time.time;
            };
        }

        private static SkillSlot GuessSlot(CharacterBody attacker, DamageInfo damageInfo)
        {
            DamageSource source = damageInfo.damageType.damageSource;
            // Only untagged, direct hits: not DoTs, hazards, equipment or item procs.
            if (source != DamageSource.NoneSpecified || damageInfo.procChainMask.mask != 0) return SkillSlot.None;
            if (!lastSkill.TryGetValue(attacker, out LastSkill entry) || Time.time - entry.time > AttributionWindow) return SkillSlot.None;
            return entry.slot;
        }

        #endregion

        /// <summary>True when the player may buy levels of this skill.</summary>
        internal static bool CanUpgrade(string skillName)
        {
            return SkillModifierManager.HasSkillModifier(skillName) || (GenericEnabled.Value && !string.IsNullOrEmpty(skillName));
        }

        /// <summary>True when the skill has no dedicated modifier and runs on generic upgrades only.</summary>
        internal static bool IsGeneric(string skillName)
        {
            return GenericEnabled.Value && !string.IsNullOrEmpty(skillName) && !SkillModifierManager.HasSkillModifier(skillName);
        }

        /// <summary>True when the skill gets generic cooldown/stock: no dedicated modifier, or one that opts in.</summary>
        internal static bool UsesGenericCooldownAndStock(string skillName)
        {
            if (!GenericEnabled.Value || string.IsNullOrEmpty(skillName)) return false;
            var modifier = SkillModifierManager.GetSkillModifierByName(skillName);
            return modifier == null || modifier.UseGenericCooldownAndStock;
        }

        internal static float DefaultDamagePerLevel(SkillSlot slot)
        {
            switch (slot)
            {
                case SkillSlot.Primary: return PrimaryDamage.Value;
                case SkillSlot.Secondary:
                case SkillSlot.Utility: return SecondaryUtilityDamage.Value;
                case SkillSlot.Special: return SpecialDamage.Value;
                default: return 0f;
            }
        }

        private static float DamageBonusPerLevel(string skillName, SkillSlot slot)
        {
            var modifier = SkillModifierManager.GetSkillModifierByName(skillName);
            if (modifier != null)
            {
                return modifier.SlotDamageBonusPerLevel(slot);
            }
            return IsGeneric(skillName) ? DefaultDamagePerLevel(slot) : 0f;
        }

        private static SkillSlot SlotFromDamageSource(DamageSource source)
        {
            if ((source & DamageSource.Primary) != 0) return SkillSlot.Primary;
            if ((source & DamageSource.Secondary) != 0) return SkillSlot.Secondary;
            if ((source & DamageSource.Utility) != 0) return SkillSlot.Utility;
            if ((source & DamageSource.Special) != 0) return SkillSlot.Special;
            return SkillSlot.None;
        }

        private static GenericSkill SkillInSlot(CharacterBody body, SkillSlot slot)
        {
            SkillLocator locator = body ? body.skillLocator : null;
            if (!locator) return null;
            switch (slot)
            {
                case SkillSlot.Primary: return locator.primary;
                case SkillSlot.Secondary: return locator.secondary;
                case SkillSlot.Utility: return locator.utility;
                case SkillSlot.Special: return locator.special;
                default: return null;
            }
        }

        private static void TakeDamageProcess(On.RoR2.HealthComponent.orig_TakeDamageProcess orig, HealthComponent self, DamageInfo damageInfo)
        {
            if (NetworkServer.active && damageInfo != null && damageInfo.attacker && damageInfo.damage > 0f
                && damageInfo.attacker.TryGetComponent(out CharacterBody attacker))
            {
                var controller = SkillPointsController.FromBody(attacker);
                if (controller && controller.isSurvivorEnabled)
                {
                    SkillSlot slot = SlotFromDamageSource(damageInfo.damageType.damageSource);
                    bool guessed = false;
                    if (slot == SkillSlot.None)
                    {
                        slot = GuessSlot(attacker, damageInfo);
                        guessed = slot != SkillSlot.None;
                    }
                    GenericSkill skill = SkillInSlot(attacker, slot);
                    string name = SkillUpgrade.SkillName(skill ? skill.skillDef : null);
                    // A guessed slot only counts for skills that rely on generic upgrades, so vanilla skills
                    // (which tag their damage properly) are never double-dipped by item damage.
                    int level = guessed && !IsGeneric(name) && SkillModifierManager.GetSkillModifierByName(name) == null ? 0 : controller.GetLevel(name);
                    if (level > 0)
                    {
                        float perLevel = DamageBonusPerLevel(name, slot);
                        if (perLevel > 0f)
                        {
                            damageInfo.damage *= Mathf.Pow(1f + perLevel, level);
                        }
                    }
                }
            }
            orig(self, damageInfo);
        }

        private static void GetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            var controller = SkillPointsController.FromBody(sender);
            if (!controller || !controller.isSurvivorEnabled || GenericCooldown.Value <= 0f) return;

            float Reduction(GenericSkill skill)
            {
                string name = SkillUpgrade.SkillName(skill ? skill.skillDef : null);
                int level = controller.GetLevel(name);
                return level > 0 && UsesGenericCooldownAndStock(name) ? 1f - Mathf.Pow(1f - GenericCooldown.Value, level) : 0f;
            }

            SkillLocator locator = sender.skillLocator;
            if (!locator) return;
            args.primaryCooldownMultAdd -= Reduction(locator.primary);
            args.secondaryCooldownMultAdd -= Reduction(locator.secondary);
            args.utilityCooldownMultAdd -= Reduction(locator.utility);
            args.specialCooldownMultAdd -= Reduction(locator.special);
        }

        // How much bonus stock we added to each skill last time, so we can take it back off before recalculating.
        // (The game only resets bonus stock for secondary/utility/special, never for primary.)
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GenericSkill, StrongBox> addedStock =
            new System.Runtime.CompilerServices.ConditionalWeakTable<GenericSkill, StrongBox>();

        private sealed class StrongBox { public int value; }

        private static void RecalculateStats(On.RoR2.CharacterBody.orig_RecalculateStats orig, CharacterBody self)
        {
            GenericSkill[] skills = self && self.skillLocator
                ? new[] { self.skillLocator.primary, self.skillLocator.secondary, self.skillLocator.utility, self.skillLocator.special }
                : Array.Empty<GenericSkill>();

            foreach (GenericSkill skill in skills)
            {
                if (skill && addedStock.TryGetValue(skill, out StrongBox box) && box.value != 0)
                {
                    skill.SetBonusStockFromBody(Math.Max(0, skill.bonusStockFromBody - box.value));
                    box.value = 0;
                }
            }

            orig(self);

            int every = GenericLevelsPerStock.Value;
            var controller = SkillPointsController.FromBody(self);
            if (every <= 0 || !controller || !controller.isSurvivorEnabled) return;

            foreach (GenericSkill skill in skills)
            {
                if (!skill) continue;
                string name = SkillUpgrade.SkillName(skill.skillDef);
                if (!UsesGenericCooldownAndStock(name)) continue;
                int bonus = controller.GetLevel(name) / every;
                if (bonus > 0)
                {
                    skill.SetBonusStockFromBody(skill.bonusStockFromBody + bonus);
                    addedStock.GetOrCreateValue(skill).value = bonus;
                }
            }
        }
    }
}
