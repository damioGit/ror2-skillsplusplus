using System;
using System.Collections.Generic;
using RoR2;
using RoR2.Projectile;
using UnityEngine;
using UnityEngine.Networking;

namespace SkillsPlusPlus.Util
{
    /// <summary>
    /// Per-instance projectile upgrades applied on the host.
    ///
    /// Projectiles are spawned by the host from their prefab, so changing a prefab on a client does nothing and
    /// changing it on the host would affect every player. Instead, a modifier registers a tweak for a projectile
    /// prefab, and when an instance spawns on the host the tweak runs on that instance with its owner's level.
    /// </summary>
    internal static class ProjectileTweaks
    {
        private struct Entry
        {
            public string[] skillNames;
            public Action<GameObject, CharacterBody, int> tweak;
        }

        private static readonly Dictionary<string, Entry> byPrefabName = new Dictionary<string, Entry>(StringComparer.Ordinal);

        // Tweaks keyed by a component type name, for projectiles from other mods whose prefabs we can't reference.
        private static readonly List<KeyValuePair<Type, Entry>> byComponent = new List<KeyValuePair<Type, Entry>>();

        /// <summary>Registers a tweak for any projectile carrying a component of <paramref name="componentType"/>.</summary>
        internal static void RegisterByComponent(Type componentType, Action<GameObject, CharacterBody, int> tweak, params string[] skillNames)
        {
            if (componentType == null || tweak == null) return;
            byComponent.Add(new KeyValuePair<Type, Entry>(componentType, new Entry { skillNames = skillNames, tweak = tweak }));
        }

        internal static void InstallHooks()
        {
            On.RoR2.Projectile.ProjectileController.Start += ProjectileController_Start;
        }

        /// <summary>Registers (or replaces) a tweak for instances of <paramref name="prefab"/>.</summary>
        internal static void Register(GameObject prefab, Action<GameObject, CharacterBody, int> tweak, params string[] skillNames)
        {
            if (!prefab || tweak == null) return;
            byPrefabName[prefab.name] = new Entry { skillNames = skillNames, tweak = tweak };
        }

        private static void ProjectileController_Start(On.RoR2.Projectile.ProjectileController.orig_Start orig, ProjectileController self)
        {
            if (NetworkServer.active && self && self.owner && byComponent.Count > 0 && self.owner.TryGetComponent(out CharacterBody componentOwner))
            {
                foreach (var pair in byComponent)
                {
                    if (!self.GetComponent(pair.Key)) continue;
                    int level = SkillUpgrade.GetLevel(componentOwner, pair.Value.skillNames);
                    if (level <= 0) continue;
                    try { pair.Value.tweak(self.gameObject, componentOwner, level); }
                    catch (Exception e) { Logger.Error(e); }
                }
            }
            if (NetworkServer.active && self && self.owner && byPrefabName.Count > 0)
            {
                string name = self.gameObject.name;
                int clone = name.IndexOf("(Clone)", StringComparison.Ordinal);
                if (clone >= 0) name = name.Substring(0, clone);
                if (byPrefabName.TryGetValue(name, out Entry entry) && self.owner.TryGetComponent(out CharacterBody owner))
                {
                    int level = SkillUpgrade.GetLevel(owner, entry.skillNames);
                    if (level > 0)
                    {
                        try
                        {
                            entry.tweak(self.gameObject, owner, level);
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e);
                        }
                    }
                }
            }
            orig(self);
        }
    }

    /// <summary>Remembers the first-seen (vanilla) value of static tunables so upgrades always scale from vanilla.</summary>
    internal static class Originals
    {
        private static readonly Dictionary<string, float> floats = new Dictionary<string, float>();
        private static readonly Dictionary<string, int> ints = new Dictionary<string, int>();

        internal static float Get(string key, float current)
        {
            if (!floats.TryGetValue(key, out float value))
            {
                floats[key] = current;
                return current;
            }
            return value;
        }

        internal static int Get(string key, int current)
        {
            if (!ints.TryGetValue(key, out int value))
            {
                ints[key] = current;
                return current;
            }
            return value;
        }
    }
}
