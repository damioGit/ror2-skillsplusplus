using BepInEx.Configuration;
using HarmonyLib;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using RoR2;

namespace SkillsPlusPlus.Director
{
    /// <summary>
    /// Optional "fewer, readable fights" module. Host-only in effect: spawn logic only runs on the host.
    ///
    /// - Caps how many monster-team and void-team characters can be alive at once (vanilla 40 each).
    ///   The game enforces this cap in MasterSummon for every spawn source.
    /// - Makes the combat director pick elite waves less often by raising its eliteBias only while it chooses
    ///   the elite tier. The spawn's credit cost is unchanged, so credits not spent on elites go to normal
    ///   monsters and gold/XP stay roughly the same.
    /// </summary>
    internal static class CalmerDirector
    {
        private const int VanillaLimit = 40;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> MonsterLimit;
        internal static ConfigEntry<int> VoidLimit;
        internal static ConfigEntry<float> EliteBiasMultiplier;

        internal static void Init(ConfigFile config)
        {
            Enabled = config.Bind("Calmer Director", "Enabled", false,
                "Fewer monsters alive at once and fewer elites. Only the host's setting matters.");
            MonsterLimit = config.Bind("Calmer Director", "Monster limit", 25,
                "Max monster-team characters alive at once. Vanilla = 40.");
            VoidLimit = config.Bind("Calmer Director", "Void limit", 25,
                "Max void-team characters alive at once (Void Seeds etc.). Vanilla = 40.");
            EliteBiasMultiplier = config.Bind("Calmer Director", "Elite rarity", 1.5f,
                "How many times more credits the director needs before it picks an elite wave. 1 = vanilla, 1.5 = noticeably fewer elites, 2 = much fewer.");

            ModSettingsManager.AddOption(new CheckBoxOption(Enabled));
            ModSettingsManager.AddOption(new IntSliderOption(MonsterLimit, new IntSliderConfig { min = 5, max = 60 }));
            ModSettingsManager.AddOption(new IntSliderOption(VoidLimit, new IntSliderConfig { min = 5, max = 60 }));
            ModSettingsManager.AddOption(new StepSliderOption(EliteBiasMultiplier, new StepSliderConfig { min = 1f, max = 3f, increment = 0.1f }));

            Run.onRunStartGlobal += _ => ApplyLimits();
            config.SettingChanged += (_, __) => ApplyLimits();

            new Harmony("com.cwmlolzlz.skills.calmerdirector").PatchAll(typeof(PrepareNewMonsterWavePatch));
        }

        private static void ApplyLimits()
        {
            bool on = Enabled.Value;
            SetLimit(TeamIndex.Monster, on ? MonsterLimit.Value : VanillaLimit);
            SetLimit(TeamIndex.Void, on ? VoidLimit.Value : VanillaLimit);
        }

        private static void SetLimit(TeamIndex team, int value)
        {
            TeamDef def = TeamCatalog.GetTeamDef(team);
            if (def != null && value > 0) def.softCharacterLimit = value;
        }

        [HarmonyPatch(typeof(CombatDirector), nameof(CombatDirector.PrepareNewMonsterWave))]
        private static class PrepareNewMonsterWavePatch
        {
            private static void Prefix(CombatDirector __instance, out float __state)
            {
                __state = __instance.eliteBias;
                if (Enabled.Value) __instance.eliteBias *= EliteBiasMultiplier.Value;
            }

            private static void Postfix(CombatDirector __instance, float __state)
            {
                __instance.eliteBias = __state;
            }
        }
    }
}
