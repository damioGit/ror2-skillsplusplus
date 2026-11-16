using System;
using System.Reflection;
using BepInEx.Configuration;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace SkillsPlusPlus.Modifiers
{
    /// <summary>
    /// Seamstress level-up mechanic, "Threading": hits with Trim or Flurry have a 5% chance per level (scaled by proc
    /// coefficient) to sew a Needle, up to her normal needle cap. Needles power Clip's extra snips and her jump
    /// needle volleys, so investing in the primary feeds the rest of her kit.
    ///
    /// Needles are a buff, so this runs on the host. Seamstress types are looked up by name, so nothing happens when
    /// the Seamstress mod is not installed.
    /// </summary>
    internal static class SeamstressNeedles
    {
        private const float ChancePerLevel = 5f;
        private static readonly string[] PrimarySkills = { "Trim", "Flurry" };

        private static BuffDef needlesBuff;
        private static ConfigEntry<int> maxNeedles;
        private static bool resolved;

        internal static void Init()
        {
            On.RoR2.GlobalEventManager.OnHitEnemy += OnHitEnemy;
        }

        private static bool Resolve()
        {
            if (resolved) return needlesBuff != null;
            resolved = true;
            Type buffs = FindType("SeamstressMod.Seamstress.Content.SeamstressBuffs");
            Type config = FindType("SeamstressMod.Seamstress.Content.SeamstressConfig");
            needlesBuff = buffs?.GetField("Needles", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as BuffDef;
            maxNeedles = config?.GetField("maxNeedleAmount", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as ConfigEntry<int>;
            return needlesBuff != null;
        }

        private static void OnHitEnemy(On.RoR2.GlobalEventManager.orig_OnHitEnemy orig, GlobalEventManager self, DamageInfo damageInfo, GameObject victim)
        {
            orig(self, damageInfo, victim);
            if (!NetworkServer.active || damageInfo == null || !damageInfo.attacker || damageInfo.procCoefficient <= 0f) return;
            if ((damageInfo.damageType.damageSource & DamageSource.Primary) == 0) return;
            if (!damageInfo.attacker.TryGetComponent(out CharacterBody attacker)) return;

            int level = SkillUpgrade.GetLevel(attacker, PrimarySkills);
            if (level <= 0 || !Resolve()) return;

            int cap = maxNeedles != null ? maxNeedles.Value : 5;
            if (attacker.GetBuffCount(needlesBuff) >= cap) return;
            if (RoR2.Util.CheckRoll(ChancePerLevel * level * damageInfo.procCoefficient, attacker.master))
            {
                attacker.AddBuff(needlesBuff);
            }
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
    }
}
