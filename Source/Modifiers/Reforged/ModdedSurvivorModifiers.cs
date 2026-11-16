using System;
using System.Reflection;
using EntityStates;
using RoR2;
using RoR2.Skills;
using SkillsPlusPlus.Generic;
using SkillsPlusPlus.Util;
using UnityEngine;

// Upgrades for survivors from other mods (Starstorm 2, Sandswept, Seamstress). Their entity state and component
// types are looked up by name after every plugin has loaded, and their fields are set by reflection, so Skills++
// keeps working when those mods are missing or rename something (that skill then falls back to generic upgrades).
//
// Modded skills keep the generic cooldown reduction and extra charges on top of their mechanic, and get the
// default per-slot damage bonus.
namespace SkillsPlusPlus.Modifiers
{
    internal sealed class ReflectedSkillModifier : BaseSkillModifier
    {
        internal Action<BaseState, Type, int> onEnter;
        internal Action<BaseState, Type, int> onEnterLate;
        internal Action<SkillDef, int> onLeveledUp;

        public override float SlotDamageBonusPerLevel(SkillSlot slot) => SlotUpgrades.DefaultDamagePerLevel(slot);

        public override bool UseGenericCooldownAndStock => true;

        public override void OnSkillEnter(BaseState skillState, int level)
        {
            base.OnSkillEnter(skillState, level);
            onEnter?.Invoke(skillState, skillState.GetType(), level);
        }

        public override void OnSkillEnterLate(BaseState skillState, int level)
        {
            onEnterLate?.Invoke(skillState, skillState.GetType(), level);
        }

        public override void OnSkillLeveledUp(int level, CharacterBody characterBody, SkillDef skillDef)
        {
            base.OnSkillLeveledUp(level, characterBody, skillDef);
            onLeveledUp?.Invoke(skillDef, level);
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
            Add("FireTriLeaf", new[] { "EntityStates.Chirr.FireTriLeaf" }, enter: (s, t, l) => AddStaticInt(t, "numShots", l));
            Add("GrabDash", new[] { "EntityStates.Chirr.GrabDash" }, enter: (s, t, l) => ScaleStatic(t, "maxGrabRadius", 0.15f, l));

            // --- Nemesis Commando (Starstorm 2)
            Add("NemCommandoSubmission", new[] { "EntityStates.NemCommando.Submission" }, enter: (s, t, l) => AddStaticInt(t, "BulletCount", l / 2));
            Add("NemCommandoSwordBeam", new[] { "EntityStates.NemCommando.SwordBeam" }, enter: (s, t, l) => ScaleStatic(t, "projectileSpeed", 0.15f, l));
            Add("NemCommandoBossAttack", new[] { "EntityStates.Nemmando.BossAttackCharge" }, enter: (s, t, l) => ScaleStatic(t, "baseChargeDuration", -0.12f, l));

            // --- Nemesis Mercenary (Starstorm 2)
            Add("NemMercSpecial", new[] { "EntityStates.NemMerc.Scan" }, enter: (s, t, l) => AddInstanceInt(s, t, "targetedHolograms", l / 2));
            Add("sdNemMercClone", new[] { "EntityStates.NemMerc.SpawnClone" }, enter: (s, t, l) => AddStaticFloat(t, "cloneLifetime", 1.5f * l));
            Add("NemMercSlide", new[] { "EntityStates.NemMerc.Slide" }, enter: (s, t, l) => ScaleStatic(t, "baseDuration", 0.12f, l));
            Add("Knoife", new[] { "EntityStates.NemMerc.ThrowKnife" }, enter: (s, t, l) => ScaleStatic(t, "autoAimRadius", 0.25f, l));

            // --- Executioner (Starstorm 2)
            Add("sdExe2Pistol", new[] { "EntityStates.Executioner.FirePistol" }, enter: (s, t, l) => ScaleStatic(t, "bulletRadius", 0.10f, l));
            Add("sdExe2FireTaser", new[] { "EntityStates.Executioner2.FireTaser" }, enterLate: (s, t, l) =>
            {
                ScaleInstance(s, t, "attackRange", 0.10f, l);
                AddInstanceInt(s, t, "attackFireCount", l / 2);
            });
            Add("sdExe2ChargeIons", new[] { "EntityStates.Executioner2.ChargeGun", "EntityStates.Executioner2.FireChargeGun" },
                leveledUp: (def, l) => def.baseMaxStock = Originals.Get("Exe2.Ions.stock", def.baseMaxStock) + l / 2);
            Add("sdExe2ChargeConsecration", new[] { "EntityStates.Executioner2.ChargeConsecration", "EntityStates.Executioner2.Consecration" }, enter: (s, t, l) =>
            {
                AddStaticFloat(t, "cooldownDeduction", 0.2f * l);
                AddStaticFloat(t, "healFraction", 0.01f * l);
            });
            Add("sdExe2Dash", new[] { "EntityStates.Executioner2.Dash" }, enter: (s, t, l) =>
            {
                AddStaticFloat(t, "debuffDuration", 0.5f * l);
                ScaleStatic(t, "debuffRadius", 0.10f, l);
            });
            Add("sdExe2Slam", new[] { "EntityStates.Executioner2.ExecuteLeap", "EntityStates.Executioner2.ExecuteSlam" }, enter: (s, t, l) => ScaleStatic(t, "slamRadius", 0.12f, l));

            // --- Ranger (Sandswept). Keys are skillName because its SkillDefs have no asset name.
            Add("Galvanize", new[] { "Sandswept.Survivors.Ranger.States.Secondary.Galvanize" }, enter: (s, t, l) => AddStaticInt(t, "maxProjectiles", l));
            Add("Release", new[] { "Sandswept.Survivors.Ranger.States.Secondary.Release" }, enter: (s, t, l) => ScaleStatic(t, "maxDamageCoefficient", 0.10f, l));
            Add("Sidestep", new[] { "Sandswept.Survivors.Ranger.States.Utility.Sidestep" }, enter: (s, t, l) => ScaleStatic(t, "Duration", 0.10f, l));

            // --- VOL-T / Electrician (Sandswept)
            Add("Galvanic Bolt", new[] { "Sandswept.Survivors.Electrician.States.GalvanicBolt" });
            Component("Sandswept.Survivors.Electrician.GalvanicBallController", "Galvanic Bolt", (go, c, l) =>
            {
                ScaleField(c, "radius", 0.15f, l);
                ScaleField(c, "range", 0.15f, l);
            });
            Add("Tempest Sphere", new[] { "Sandswept.Survivors.Electrician.States.TempestSphereCharge", "Sandswept.Survivors.Electrician.States.TempestSphereFire" });
            Component("Sandswept.Survivors.Electrician.TempestBallController", "Tempest Sphere", (go, c, l) => ScaleField(c, "ticksPerSecond", 0.10f, l));
            Add("Static Snare", new[] { "Sandswept.Survivors.Electrician.States.StaticSnare" });
            Component("Sandswept.Survivors.Electrician.TripwireController", "Static Snare", (go, c, l) =>
            {
                ScaleField(c, "passiveExplosionRadius", 0.15f, l);
                ScaleField(c, "ejectExplosionRadius", 0.15f, l);
                ScaleField(c, "ejectExplosionDamage", 0.20f, l);
            });
            var overload = new[] { "Sandswept.Survivors.Electrician.States.SignalOverloadCharge", "Sandswept.Survivors.Electrician.States.SignalOverloadFire", "Sandswept.Survivors.Electrician.States.SignalOverloadDischarge" };
            Action<BaseState, Type, int> overloadLate = (s, t, l) =>
            {
                if (t.Name == "SignalOverloadDischarge")
                {
                    AddInstanceFloat(s, t, "duration", 0.4f * l);
                    ScaleInstance(s, t, "radius", 0.10f, l);
                }
                else if (t.Name == "SignalOverloadFire")
                {
                    ScaleInstance(s, t, "radius", 0.10f, l);
                }
            };
            // Sandswept names this skill "Sigma Overload" 2% of the time, so register both.
            Add("Signal Overload", overload, enterLate: overloadLate);
            Add("Sigma Overload", overload, enterLate: overloadLate);

            // --- Seamstress. Her numbers come from her own config inside OnEnter, so changes happen afterwards.
            Add("Trim", new[] { "SeamstressMod.Seamstress.SkillStates.Trim" }, enterLate: (s, t, l) => FasterSwing(s, t, 0.05f, l));
            Add("Flurry", new[] { "SeamstressMod.Seamstress.SkillStates.Flurry" }, enterLate: (s, t, l) => FasterSwing(s, t, 0.06f, l));
            Add("Clip", new[] { "SeamstressMod.Seamstress.SkillStates.Clip" }, enterLate: (s, t, l) => AddInstanceInt(s, t, "snips", l / 2));
            Add("PlanarManipulation", new[] { "SeamstressMod.Seamstress.SkillStates.PlanarManipulation" }, enterLate: (s, t, l) =>
            {
                ScaleInstance(s, t, "_maxGrabDistance", 0.15f, l);
                ScaleInstance(s, t, "_minGrabDistance", 0.15f, l);
                ScaleInstance(s, t, "radius", 0.15f, l);
            });
            Add("HeartDashSeamstress", new[] { "SeamstressMod.Seamstress.SkillStates.HealthCostDash" }, enterLate: (s, t, l) =>
            {
                ScaleInstance(s, t, "dashPower", 0.10f, l);
                AddInstanceFloat(s, t, "insatiableDuration", 0.5f * l);
            });
            Add("ParrySeamstress", new[] { "SeamstressMod.Seamstress.SkillStates.Parry" }, enterLate: (s, t, l) => AddInstanceFloat(s, t, "duration", 0.08f * l));
            Add("FireSeamstress", new[] { "SeamstressMod.Seamstress.SkillStates.FireScissors" }, enter: (s, t, l) => ScaleStatic(t, "baseDuration", -0.08f, l));

            // --- More Starstorm 2 / Sandswept skills that were on generic upgrades only
            Add("NemCommandoSwingSword", new[] { "EntityStates.NemCommando.SwingSword2" }, enter: (s, t, l) => ScaleInstance(s, t, "baseDuration", -0.05f, l));
            Add("NemCommandoShootGunReload", new[] { "EntityStates.NemCommando.ShootGun2" },
                enter: (s, t, l) => ScaleStatic(t, "range", 0.20f, l),
                leveledUp: (def, l) => def.baseMaxStock = Originals.Get("NemCommando.SingleTap.stock", def.baseMaxStock) + l / 2);
            Add("NemMercShotgun", new[] { "EntityStates.NemMerc.FireShotgun" }, enter: (s, t, l) => AddInstanceInt(s, t, "bulletCount", l));
            Add("NemMercLunge", new[] { "EntityStates.NemMerc.Lunge" }, enter: (s, t, l) => ScaleStatic(t, "maximumDuration", 0.12f, l));
            // Dispatch: after blinking, a short attack speed burst (War Cry buff), 0.5 s per level. Buffs are host-side.
            Add("NemMercBlink", new[] { "EntityStates.NemMerc.ShadowStep" }, enter: (s, t, l) =>
            {
                if (UnityEngine.Networking.NetworkServer.active && s.characterBody)
                    s.characterBody.AddTimedBuff(RoR2Content.Buffs.WarCryBuff, 0.5f * l);
            });
            Add("Direct Current", new[] { "Sandswept.Survivors.Ranger.States.Primary.DirectCurrent" }, enter: (s, t, l) => ScaleStatic(t, "baseDuration", -0.05f, l));
        }

        private static void Add(string skillName, string[] stateTypeNames,
            Action<BaseState, Type, int> enter = null, Action<BaseState, Type, int> enterLate = null, Action<SkillDef, int> leveledUp = null)
        {
            var types = new System.Collections.Generic.List<Type>();
            foreach (string name in stateTypeNames)
            {
                Type type = FindType(name);
                if (type != null) types.Add(type);
            }
            if (types.Count == 0)
            {
                Logger.Debug("Modded skill {0}: no state found, using generic upgrades", skillName);
                return;
            }
            var modifier = new ReflectedSkillModifier { onEnter = enter, onEnterLate = enterLate, onLeveledUp = leveledUp };
            SkillModifierManager.RegisterModifier(modifier, new[] { skillName }, types.ToArray());
        }

        private static void Component(string componentTypeName, string skillName, Action<GameObject, Component, int> tweak)
        {
            Type type = FindType(componentTypeName);
            if (type == null) return;
            ProjectileTweaks.RegisterByComponent(type, (go, owner, level) =>
            {
                Component component = go.GetComponent(type);
                if (component) tweak(go, component, level);
            }, skillName);
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

        private static FieldInfo Field(Type type, string name, BindingFlags flags)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                FieldInfo info = t.GetField(name, flags | BindingFlags.DeclaredOnly);
                if (info != null) return info;
            }
            return null;
        }

        private static void ScaleStatic(Type type, string field, float perLevel, int level)
        {
            FieldInfo info = Field(type, field, Static);
            if (info == null || info.FieldType != typeof(float)) return;
            float original = Originals.Get(Key(type, field), (float)info.GetValue(null));
            info.SetValue(null, BaseSkillModifier.MultScaling(original, perLevel, level));
        }

        private static void AddStaticFloat(Type type, string field, float amount)
        {
            FieldInfo info = Field(type, field, Static);
            if (info == null || info.FieldType != typeof(float)) return;
            float original = Originals.Get(Key(type, field), (float)info.GetValue(null));
            info.SetValue(null, original + amount);
        }

        private static void AddStaticInt(Type type, string field, int amount)
        {
            FieldInfo info = Field(type, field, Static);
            if (info == null || info.FieldType != typeof(int)) return;
            int original = Originals.Get(Key(type, field), (int)info.GetValue(null));
            info.SetValue(null, original + amount);
        }

        private static void ScaleInstance(object target, Type type, string field, float perLevel, int level)
        {
            FieldInfo info = Field(type, field, Instance);
            if (info == null || info.FieldType != typeof(float)) return;
            info.SetValue(target, BaseSkillModifier.MultScaling((float)info.GetValue(target), perLevel, level));
        }

        private static void AddInstanceFloat(object target, Type type, string field, float amount)
        {
            FieldInfo info = Field(type, field, Instance);
            if (info == null || info.FieldType != typeof(float)) return;
            info.SetValue(target, (float)info.GetValue(target) + amount);
        }

        private static void AddInstanceInt(object target, Type type, string field, int amount)
        {
            FieldInfo info = Field(type, field, Instance);
            if (info == null || info.FieldType != typeof(int)) return;
            info.SetValue(target, (int)info.GetValue(target) + amount);
        }

        private static void ScaleField(Component component, string field, float perLevel, int level)
        {
            ScaleInstance(component, component.GetType(), field, perLevel, level);
        }

        /// <summary>Shortens a Seamstress melee swing (its duration and scissor duration) by perLevel per level.</summary>
        private static void FasterSwing(BaseState state, Type type, float perLevel, int level)
        {
            ScaleInstance(state, type, "duration", -perLevel, level);
            ScaleInstance(state, type, "scissorDuration", -perLevel, level);
        }
    }
}
