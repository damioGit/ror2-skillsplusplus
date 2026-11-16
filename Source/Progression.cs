using BepInEx.Configuration;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace SkillsPlusPlus
{
    /// <summary>
    /// Respec and extra sources of skill points. All decisions are made on the host with the host's settings.
    ///
    /// - Respec: press the respec key (default F8) or type <c>spp_respec</c> in the console. By default only in the
    ///   Bazaar Between Time and for 1 lunar coin; both are configurable.
    /// - Bonus points: optionally for every charged teleporter and every N cleared stages.
    /// </summary>
    internal static class Progression
    {
        internal static ConfigEntry<KeyboardShortcut> RespecKey;
        internal static ConfigEntry<bool> RespecOnlyInBazaar;
        internal static ConfigEntry<int> RespecLunarCoinCost;
        internal static ConfigEntry<int> PointsPerTeleporter;
        internal static ConfigEntry<int> StagesPerBonusPoint;

        internal static void Init(ConfigFile config)
        {
            RespecKey = config.Bind("Respec", "Respec key", new KeyboardShortcut(KeyCode.F8),
                "Press to refund all your spent skill points (see the other respec settings for where and for how much).");
            RespecOnlyInBazaar = config.Bind("Respec", "Only in the Bazaar", true,
                "Respec is only allowed in the Bazaar Between Time. The host's setting is used.");
            RespecLunarCoinCost = config.Bind("Respec", "Lunar coin cost", 1,
                "Lunar coins a respec costs. 0 = free. The host's setting is used.");
            PointsPerTeleporter = config.Bind("Bonus points", "Points per charged teleporter", 0,
                "Extra skill points every player gets when the teleporter finishes charging. The host's setting is used.");
            StagesPerBonusPoint = config.Bind("Bonus points", "Stages per bonus point", 0,
                "Every player gets an extra skill point every this many cleared stages. 0 = off. The host's setting is used.");

            ModSettingsManager.AddOption(new KeyBindOption(RespecKey));
            ModSettingsManager.AddOption(new CheckBoxOption(RespecOnlyInBazaar));
            ModSettingsManager.AddOption(new IntSliderOption(RespecLunarCoinCost, new IntSliderConfig { min = 0, max = 10 }));
            ModSettingsManager.AddOption(new IntSliderOption(PointsPerTeleporter, new IntSliderConfig { min = 0, max = 5 }));
            ModSettingsManager.AddOption(new IntSliderOption(StagesPerBonusPoint, new IntSliderConfig { min = 0, max = 10 }));

            TeleporterInteraction.onTeleporterChargedGlobal += OnTeleporterCharged;
            Stage.onServerStageComplete += OnServerStageComplete;
        }

        /// <summary>Polled from the plugin's Update: the local player pressed the respec key.</summary>
        internal static void PollRespecKey()
        {
            if (!RespecKey.Value.IsDown()) return;
            RequestLocalRespec();
        }

        [ConCommand(commandName = "spp_respec", flags = ConVarFlags.None, helpText = "Refund all your spent Skills++ points (host rules apply).")]
        private static void CCRespec(ConCommandArgs args)
        {
            RequestLocalRespec();
        }

        private static void RequestLocalRespec()
        {
            foreach (var user in NetworkUser.readOnlyLocalPlayersList)
            {
                CharacterMaster master = user ? user.master : null;
                if (master && master.TryGetComponent(out SkillPointsController controller))
                {
                    controller.RequestRespec();
                }
            }
        }

        /// <summary>Host: tells everyone in chat whether a respec went through.</summary>
        internal static void AnnounceRespec(SkillPointsController controller, bool success, string reason)
        {
            if (!NetworkServer.active || !controller) return;
            var pcmc = controller.GetComponent<PlayerCharacterMasterController>();
            string name = pcmc && pcmc.networkUser ? pcmc.networkUser.userName : "Player";
            string message = success
                ? $"<color=#e5c962>{name} reset their Skills++ upgrades.</color>"
                : $"<color=#c4c4c4>{name} can't respec: {reason}.</color>";
            Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = message });
        }

        private static void OnTeleporterCharged(TeleporterInteraction teleporter)
        {
            if (!NetworkServer.active || PointsPerTeleporter.Value <= 0) return;
            GiveEveryone(PointsPerTeleporter.Value);
        }

        private static void OnServerStageComplete(Stage stage)
        {
            int every = StagesPerBonusPoint.Value;
            if (!NetworkServer.active || every <= 0 || !Run.instance) return;
            // stageClearCount is updated after this event, so count the stage being completed.
            int cleared = Run.instance.stageClearCount + 1;
            if (cleared % every == 0) GiveEveryone(1);
        }

        private static void GiveEveryone(int amount)
        {
            foreach (var player in PlayerCharacterMasterController.instances)
            {
                if (player && player.master && player.master.TryGetComponent(out SkillPointsController controller))
                {
                    controller.ServerGiveBonusPoint(amount);
                }
            }
        }
    }
}
