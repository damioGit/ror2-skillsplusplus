using System;
using System.Linq;
using EntityStates;
using RoR2;
using RoR2.Projectile;
using RoR2.Skills;
using SkillsPlusPlus.Modifiers;
using UnityEngine;

namespace SkillsPlusPlus
{
    /// <summary>
    /// One per skill slot on every survivor body. Reads its level from the owner's <see cref="SkillPointsController"/>
    /// (which the host keeps in sync) and drives the matching <see cref="BaseSkillModifier"/>.
    ///
    /// Modifiers that change shared/static values (<see cref="BaseSkillModifier.OnSkillLeveledUp"/>) only run on the
    /// machine that controls this body, and they are re-applied right before each skill state starts. That way the host
    /// never overwrites its own values with a client's levels, and two players on one machine don't clobber each other.
    /// </summary>
    [RequireComponent(typeof(CharacterBody))]
    public class SkillUpgrade : MonoBehaviour
    {
        /// <summary>Current level of the skill in this slot. Kept as a field for API compatibility.</summary>
        public int skillLevel;

        public SkillPointsController skillPointsController { get; private set; }
        public GenericSkill targetGenericSkill;
        public string targetBaseSkillName;

        private CharacterBody characterBody;

        private bool isSurvivorEnabled => skillPointsController && skillPointsController.isSurvivorEnabled;

        private void Awake()
        {
            characterBody = GetComponent<CharacterBody>();
            targetBaseSkillName = SkillName(targetGenericSkill ? targetGenericSkill.skillDef : null);
        }

        private void OnEnable()
        {
            if (targetGenericSkill) targetGenericSkill.onSkillChanged += OnSkillChanged;
            RefreshUpgrades();
        }

        private void OnDisable()
        {
            if (targetGenericSkill) targetGenericSkill.onSkillChanged -= OnSkillChanged;
        }

        public void SetSkillPointsController(SkillPointsController controller)
        {
            skillPointsController = controller;
            RefreshUpgrades();
        }

        private void OnSkillChanged(GenericSkill genericSkill)
        {
            targetBaseSkillName = SkillName(genericSkill ? genericSkill.skillDef : null);
            RefreshUpgrades();
        }

        /// <summary>Re-reads the level from the controller and re-applies the modifier on the owning machine.</summary>
        public void RefreshUpgrades()
        {
            skillLevel = skillPointsController ? skillPointsController.GetLevel(targetBaseSkillName) : 0;
            if (!isSurvivorEnabled) return;
            ApplyLevelLocally();
        }

        private void ApplyLevelLocally()
        {
            if (!characterBody || !characterBody.hasEffectiveAuthority) return;
            SkillDef activeSkillDef = GetActiveSkillDef(targetGenericSkill);
            if (activeSkillDef == null) return;
            var modifier = SkillModifierManager.GetSkillModifier(activeSkillDef);
            if (modifier == null) return;
            modifier.characterBody = characterBody;
            modifier.registeredSkill = this;
            modifier.OnSkillLeveledUp(skillLevel, characterBody, activeSkillDef);
            if (targetGenericSkill) targetGenericSkill.RecalculateValues();
        }

        public void OnBuySkill()
        {
            if (skillPointsController && CanUpgradeSkill())
            {
                skillPointsController.RequestBuy(targetBaseSkillName);
            }
        }

        public bool CanUpgradeSkill()
        {
            if (!isSurvivorEnabled || targetGenericSkill == null) return false;
            if (!skillPointsController.hasUnspentPoints) return false;
            return SkillModifierManager.HasSkillModifier(targetBaseSkillName);
        }

        /// <summary>Level of the given skill (by SkillDef name) on this body, or 0.</summary>
        public static int GetLevel(CharacterBody body, params string[] skillNames)
        {
            var controller = SkillPointsController.FromBody(body);
            if (!controller) return 0;
            int level = 0;
            foreach (string name in skillNames)
            {
                level = Math.Max(level, controller.GetLevel(name));
            }
            return level;
        }

        /// <summary>Highest level of the given skill among all players. For effects that can't be traced back to one player.</summary>
        public static int GetHighestLevelAmongPlayers(params string[] skillNames)
        {
            int level = 0;
            foreach (var player in PlayerCharacterMasterController.instances)
            {
                if (player && player.master && player.master.TryGetComponent(out SkillPointsController controller))
                {
                    foreach (string name in skillNames)
                    {
                        level = Math.Max(level, controller.GetLevel(name));
                    }
                }
            }
            return level;
        }

        internal static string SkillName(SkillDef skillDef) => skillDef ? ((ScriptableObject)skillDef).name : null;

        private static SkillDef GetActiveSkillDef(GenericSkill genericSkill)
        {
            if (genericSkill == null) return null;
            return genericSkill.skillDef ? genericSkill.skillDef : genericSkill.baseSkill;
        }

        #region Entity state hooks (installed once, globally)

        internal static void InstallHooks()
        {
            On.EntityStates.BaseState.OnEnter += OnBaseStateEnter;
            On.EntityStates.EntityState.OnExit += OnBaseStateExit;
        }

        /// <summary>Finds the upgrade on the owning survivor whose active skill this state belongs to.</summary>
        private static SkillUpgrade FindUpgradeForState(EntityState state, BaseSkillModifier modifier)
        {
            CharacterBody owner = FindOwningCharacterBody(state);
            if (!owner) return null;
            foreach (var upgrade in owner.GetComponents<SkillUpgrade>())
            {
                if (!upgrade.isSurvivorEnabled) continue;
                string name = SkillName(GetActiveSkillDef(upgrade.targetGenericSkill));
                if (name != null && modifier.skillNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    return upgrade;
                }
            }
            return null;
        }

        private static void OnBaseStateEnter(On.EntityStates.BaseState.orig_OnEnter orig, BaseState self)
        {
            if (self != null)
            {
                var modifier = SkillModifierManager.GetSkillModifiersForEntityStateType(self.GetType());
                if (modifier != null)
                {
                    var upgrade = FindUpgradeForState(self, modifier);
                    if (upgrade)
                    {
                        // Make sure shared values reflect THIS player's level before the state reads them.
                        upgrade.ApplyLevelLocally();
                        modifier.characterBody = upgrade.characterBody;
                        modifier.registeredSkill = upgrade;
                        try
                        {
                            modifier.OnSkillEnter(self, upgrade.skillLevel);
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

        private static void OnBaseStateExit(On.EntityStates.EntityState.orig_OnExit orig, EntityState self)
        {
            if (self is BaseState baseState)
            {
                var modifier = SkillModifierManager.GetSkillModifiersForEntityStateType(baseState.GetType());
                if (modifier != null)
                {
                    var upgrade = FindUpgradeForState(baseState, modifier);
                    if (upgrade)
                    {
                        modifier.characterBody = upgrade.characterBody;
                        modifier.registeredSkill = upgrade;
                        try
                        {
                            modifier.OnSkillExit(baseState, upgrade.skillLevel);
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

        /// <summary>The survivor that owns a state: the body itself, or the owner of a projectile, minion or deployable.</summary>
        internal static CharacterBody FindOwningCharacterBody(EntityState state)
        {
            if (state == null || !state.outer) return null;
            var outer = state.outer;

            if (outer.TryGetComponent(out CharacterBody body))
            {
                if (body.master && body.master.TryGetComponent(out MinionOwnership minion) && minion.ownerMaster)
                {
                    return minion.ownerMaster.GetBody();
                }
                return body;
            }
            if (outer.TryGetComponent(out ProjectileController projectile) && projectile.owner)
            {
                return projectile.owner.GetComponent<CharacterBody>();
            }
            if (outer.TryGetComponent(out MinionOwnership minionOwnership) && minionOwnership.ownerMaster)
            {
                return minionOwnership.ownerMaster.GetBody();
            }
            if (outer.TryGetComponent(out GenericOwnership genericOwnership) && genericOwnership.ownerObject)
            {
                return genericOwnership.ownerObject.GetComponent<CharacterBody>();
            }
            if (outer.TryGetComponent(out Deployable deployable) && deployable.ownerMaster)
            {
                return deployable.ownerMaster.GetBody();
            }
            return null;
        }

        #endregion
    }
}
