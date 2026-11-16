using System;
using System.Linq;
using System.Reflection;
using EntityStates;
using RoR2;
using SkillsPlusPlus.Generic;
using SkillsPlusPlus.Util;

// Upgrades for survivors from other mods (Starstorm 2, Sandswept). Their entity state types are looked up by name
// after every plugin has loaded, and their fields are set by reflection, so Skills++ keeps working when those mods
// are not installed or rename something (that skill then simply falls back to the generic upgrades).
//
// Only skills with a clear mechanical lever get a dedicated modifier here. Everything else from those mods (and
// Seamstress, whose numbers live in its own config) uses the generic upgrades: slot damage, cooldown and charges.
namespace SkillsPlusPlus.Modifiers
{
    internal sealed class ReflectedSkillModifier : BaseSkillModifier
    {
        internal Action<BaseState, Type, int> onEnter;

        public override float SlotDamageBonusPerLevel(SkillSlot slot) => SlotUpgrades.DefaultDamagePerLevel(slot);

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            onEnter?.Invoke(skillState, skillState.GetType(), level);
        }
    }

    internal static class ModdedSurvivorModifiers
    {
        private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        /// <summary>Call once all plugins are loaded (e.g. at SurvivorCatalog init).</summary>
        internal static void Register()
        {
            // --- Chirr (Starstorm 2)
            Add("FireTriLeaf", "EntityStates.Chirr.FireTriLeaf", (s, t, l) => AddStaticInt(t, "numShots", l));
            Add("GrabDash", "EntityStates.Chirr.GrabDash", (s, t, l) => ScaleStatic(t, "maxGrabRadius", 0.15f, l));

            // --- Nemesis Commando (Starstorm 2)
            Add("NemCommandoSubmission", "EntityStates.NemCommando.Submission", (s, t, l) => AddStaticInt(t, "BulletCount", l));
            Add("NemCommandoSwordBeam", "EntityStates.NemCommando.SwordBeam", (s, t, l) => ScaleStatic(t, "projectileSpeed", 0.15f, l));
            Add("NemCommandoBossAttack", "EntityStates.Nemmando.BossAttackCharge", (s, t, l) => ScaleStatic(t, "baseChargeDuration", -0.12f, l));

            // --- Nemesis Mercenary (Starstorm 2)
            Add("NemMercSpecial", "EntityStates.NemMerc.Scan", (s, t, l) => AddInstanceInt(s, t, "targetedHolograms", l / 2));
            Add("sdNemMercClone", "EntityStates.NemMerc.SpawnClone", (s, t, l) => AddStaticFloat(t, "cloneLifetime", 1.5f * l));
            Add("NemMercSlide", "EntityStates.NemMerc.Slide", (s, t, l) => ScaleStatic(t, "baseDuration", 0.12f, l));
            Add("Knoife", "EntityStates.NemMerc.ThrowKnife", (s, t, l) => ScaleStatic(t, "autoAimRadius", 0.25f, l));

            // --- Ranger (Sandswept): skill keys are skillName because its SkillDefs have no asset name
            Add("Galvanize", "Sandswept.Survivors.Ranger.States.Secondary.Galvanize", (s, t, l) => AddStaticInt(t, "maxProjectiles", l));
        }

        private static void Add(string skillName, string stateTypeName, Action<BaseState, Type, int> onEnter)
        {
            Type type = FindType(stateTypeName);
            if (type == null)
            {
                Logger.Debug("Modded skill {0}: state {1} not found, using generic upgrades", skillName, stateTypeName);
                return;
            }
            var modifier = new ReflectedSkillModifier { onEnter = onEnter };
            SkillModifierManager.RegisterModifier(modifier, new[] { skillName }, new[] { type });
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type;
                try { type = assembly.GetType(fullName, false); }
                catch { continue; }
                if (type != null) return type;
            }
            return null;
        }

        private static string Key(Type type, string field) => type.FullName + "." + field;

        private static void ScaleStatic(Type type, string field, float perLevel, int level)
        {
            FieldInfo info = type.GetField(field, Static);
            if (info == null || info.FieldType != typeof(float)) return;
            float original = Originals.Get(Key(type, field), (float)info.GetValue(null));
            info.SetValue(null, BaseSkillModifier.MultScaling(original, perLevel, level));
        }

        private static void AddStaticFloat(Type type, string field, float amount)
        {
            FieldInfo info = type.GetField(field, Static);
            if (info == null || info.FieldType != typeof(float)) return;
            float original = Originals.Get(Key(type, field), (float)info.GetValue(null));
            info.SetValue(null, original + amount);
        }

        private static void AddStaticInt(Type type, string field, int amount)
        {
            FieldInfo info = type.GetField(field, Static);
            if (info == null || info.FieldType != typeof(int)) return;
            int original = Originals.Get(Key(type, field), (int)info.GetValue(null));
            info.SetValue(null, original + amount);
        }

        private static void AddInstanceInt(object state, Type type, string field, int amount)
        {
            FieldInfo info = type.GetField(field, Instance);
            if (info == null || info.FieldType != typeof(int)) return;
            info.SetValue(state, (int)info.GetValue(state) + amount);
        }
    }
}
