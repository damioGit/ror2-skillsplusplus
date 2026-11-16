using System;
using System.Collections.Generic;
using EntityStates;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using SkillsPlusPlus.Modifiers;
using SkillsPlusPlus.Networking;
using UnityEngine;
using UnityEngine.Networking;

namespace SkillsPlusPlus
{
    /// <summary>
    /// Lives on every player master and owns that player's skill points and skill levels.
    ///
    /// The host is authoritative: it awards points as the team levels up, validates purchases and sends the
    /// whole state to clients (<see cref="SyncSkillPointsMessage"/>). Clients only display what the host sent
    /// and ask the host to buy levels (<see cref="BuySkillMessage"/>).
    ///
    /// Levels are stored per skill name (the SkillDef asset name), so they survive body changes such as
    /// respawning, and unspent points are always "earned minus levels of the skills on the current body".
    /// </summary>
    [RequireComponent(typeof(PlayerCharacterMasterController))]
    public sealed class SkillPointsController : MonoBehaviour
    {
        private PlayerCharacterMasterController playerCharacterMasterController;
        private CharacterMaster master;

        private readonly Dictionary<string, int> skillLevels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public bool isSurvivorEnabled { get; private set; } = true;
        public int earnedSkillPoints { get; private set; }
        public int unspentSkillPoints { get; private set; }
        public int levelsPerSkillPoint { get; private set; } = 5;
        public bool multScalingLinear { get; private set; }

        public bool hasUnspentPoints => unspentSkillPoints > 0;

        internal NetworkInstanceId MasterNetId => master ? master.netId : NetworkInstanceId.Invalid;

        private CharacterBody body => master ? master.GetBody() : null;

        /// <summary>The controller of the player that owns this body, or null for non-players.</summary>
        public static SkillPointsController FromBody(CharacterBody body)
        {
            if (!body || !body.master) return null;
            return body.master.GetComponent<SkillPointsController>();
        }

        /// <summary>Current level of a skill (by SkillDef name) for this player.</summary>
        public int GetLevel(string skillName)
        {
            if (string.IsNullOrEmpty(skillName)) return 0;
            return skillLevels.TryGetValue(skillName, out int level) ? level : 0;
        }

        internal List<KeyValuePair<string, int>> GetAllLevels() => new List<KeyValuePair<string, int>>(skillLevels);

        private void Awake()
        {
            playerCharacterMasterController = GetComponent<PlayerCharacterMasterController>();
            master = GetComponent<CharacterMaster>();
            levelsPerSkillPoint = Math.Max(1, ConVars.ConVars.levelsPerSkillPoint.value);
            multScalingLinear = ConVars.ConVars.multScalingLinear.value;
        }

        private void OnEnable()
        {
            if (master) master.onBodyStart += OnBodyStart;
        }

        private void OnDisable()
        {
            if (master) master.onBodyStart -= OnBodyStart;
        }

        private void OnBodyStart(CharacterBody newBody)
        {
            if (NetworkServer.active)
            {
                isSurvivorEnabled = !ConVars.ConVars.disabledSurvivors.value.Contains(newBody.GetDisplayName());
            }
            foreach (var skillUpgrade in newBody.GetComponents<SkillUpgrade>())
            {
                skillUpgrade.SetSkillPointsController(this);
            }
            if (NetworkServer.active)
            {
                ServerRecalculatePoints(forceSync: true);
            }
        }

        #region Server

        /// <summary>Awards points for the current team level and recomputes unspent points.</summary>
        internal void ServerRecalculatePoints(bool forceSync = false)
        {
            if (!NetworkServer.active || !master) return;

            levelsPerSkillPoint = Math.Max(1, ConVars.ConVars.levelsPerSkillPoint.value);
            multScalingLinear = ConVars.ConVars.multScalingLinear.value;

            int previousEarned = earnedSkillPoints;
            int previousUnspent = unspentSkillPoints;

            TeamIndex team = master.teamIndex;
            if (team != TeamIndex.None && TeamManager.instance)
            {
                int teamLevel = (int)TeamManager.instance.GetTeamLevel(team);
                earnedSkillPoints = Math.Max(earnedSkillPoints, SkillPointsAtLevel(teamLevel));
            }
            RecomputeUnspent();

            if (forceSync || previousEarned != earnedSkillPoints || previousUnspent != unspentSkillPoints)
            {
                ServerSync();
            }
        }

        /// <summary>Validates and applies a purchase of one level of <paramref name="skillName"/>.</summary>
        internal void ServerTryBuy(string skillName)
        {
            if (!NetworkServer.active || !isSurvivorEnabled || string.IsNullOrEmpty(skillName)) return;
            CharacterBody currentBody = body;
            if (!currentBody) return;

            SkillUpgrade target = null;
            foreach (var upgrade in currentBody.GetComponents<SkillUpgrade>())
            {
                if (string.Equals(upgrade.targetBaseSkillName, skillName, StringComparison.OrdinalIgnoreCase))
                {
                    target = upgrade;
                    break;
                }
            }
            if (target == null || !Generic.SlotUpgrades.CanUpgrade(skillName)) return;

            RecomputeUnspent();
            if (unspentSkillPoints <= 0) return;

            skillLevels[skillName] = GetLevel(skillName) + 1;
            Logger.Debug("ServerTryBuy({0}) -> level {1}", skillName, skillLevels[skillName]);
            RecomputeUnspent();
            ServerSync();
            RefreshBodyUpgrades();
        }

        /// <summary>Debug helper: grants one bonus point.</summary>
        internal void ServerGiveBonusPoint()
        {
            if (!NetworkServer.active) return;
            earnedSkillPoints++;
            RecomputeUnspent();
            ServerSync();
        }

        private void RecomputeUnspent()
        {
            int spent = 0;
            CharacterBody currentBody = body;
            if (currentBody)
            {
                var counted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var upgrade in currentBody.GetComponents<SkillUpgrade>())
                {
                    string name = upgrade.targetBaseSkillName;
                    if (!string.IsNullOrEmpty(name) && counted.Add(name))
                    {
                        spent += GetLevel(name);
                    }
                }
            }
            unspentSkillPoints = Math.Max(0, earnedSkillPoints - spent);
        }

        private void ServerSync()
        {
            if (!NetworkServer.active || MasterNetId == NetworkInstanceId.Invalid) return;
            new SyncSkillPointsMessage(this).Send(NetworkDestination.Clients);
        }

        private int SkillPointsAtLevel(int characterLevel)
        {
            if (levelsPerSkillPoint <= 1) return Math.Max(0, characterLevel - 1);
            return characterLevel / levelsPerSkillPoint;
        }

        #endregion

        #region Client

        internal void ClientApplySync(bool enabled, int earned, int unspent, int levelsPer, bool linear, List<KeyValuePair<string, int>> levels)
        {
            isSurvivorEnabled = enabled;
            earnedSkillPoints = earned;
            unspentSkillPoints = unspent;
            levelsPerSkillPoint = levelsPer;
            multScalingLinear = linear;
            skillLevels.Clear();
            foreach (var pair in levels)
            {
                skillLevels[pair.Key] = pair.Value;
            }
            RefreshBodyUpgrades();
        }

        /// <summary>Asks the host to buy one level of a skill. Works for the host itself too.</summary>
        public void RequestBuy(string skillName)
        {
            if (string.IsNullOrEmpty(skillName)) return;
            if (NetworkServer.active)
            {
                ServerTryBuy(skillName);
            }
            else if (NetworkClient.active)
            {
                new BuySkillMessage(MasterNetId, skillName).Send(NetworkDestination.Server);
            }
        }

        #endregion

        private void RefreshBodyUpgrades()
        {
            CharacterBody currentBody = body;
            if (!currentBody) return;
            foreach (var upgrade in currentBody.GetComponents<SkillUpgrade>())
            {
                upgrade.RefreshUpgrades();
            }
        }

        #region Global hooks

        internal static void InstallHooks()
        {
            On.RoR2.CharacterBody.RecalculateStats += OnRecalculateStats;
            On.RoR2.CharacterMaster.GetDeployableSameSlotLimit += GetDeployableSameSlotLimit;
            On.EntityStates.GenericCharacterMain.CanExecuteSkill += CanExecuteSkill;
        }

        private static void OnRecalculateStats(On.RoR2.CharacterBody.orig_RecalculateStats orig, CharacterBody self)
        {
            orig(self);
            if (NetworkServer.active && self && self.isPlayerControlled)
            {
                FromBody(self)?.ServerRecalculatePoints();
            }
        }

        private static int GetDeployableSameSlotLimit(On.RoR2.CharacterMaster.orig_GetDeployableSameSlotLimit orig, CharacterMaster self, DeployableSlot slot)
        {
            int limit = orig(self, slot);
            if (self && self.TryGetComponent(out SkillPointsController controller) && controller.isSurvivorEnabled)
            {
                limit += EngiSkillModifier.GetDeployableSameSlotBonus(controller, slot);
            }
            return limit;
        }

        /// <summary>Blocks skill use while the "buy skills" key is held, so buying doesn't also fire the skill.</summary>
        private static bool CanExecuteSkill(On.EntityStates.GenericCharacterMain.orig_CanExecuteSkill orig, GenericCharacterMain self, GenericSkill skillSlot)
        {
            CharacterBody selfBody = self.outer ? self.outer.commonComponents.characterBody : null;
            if (selfBody && selfBody.hasEffectiveAuthority && ConVars.ConVars.disableOnBuy.value)
            {
                var controller = FromBody(selfBody);
                if (controller && controller.isSurvivorEnabled && controller.hasUnspentPoints && SkillOptions.IsBuyKeyHeld(selfBody))
                {
                    return false;
                }
            }
            return orig(self, skillSlot);
        }

        #endregion
    }
}
