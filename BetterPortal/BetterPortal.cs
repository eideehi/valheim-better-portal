using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ModUtils;

namespace BetterPortal
{
    [BepInPlugin(ModId, ModName, ModVersion)]
    public class UnityPlugin : BaseUnityPlugin
    {
        private const string ModId = "net.eidee.valheim.better_portal";
        private const string ModName = "Better Portal";
        private const string ModVersion = "1.0.6";

        private void Awake()
        {
            BetterPortal.Initialize(Info, Logger, Config);
        }
    }

    internal static class BetterPortal
    {
        public static string ModLocation { get; private set; }
        public static ModUtils.Logger Logger { get; private set; }
        public static L10N L10N { get; private set; }

        public static void Initialize(PluginInfo info, ManualLogSource logger, ConfigFile config)
        {
            ModLocation = Path.GetDirectoryName(info.Location) ?? "";
            Logger = new ModUtils.Logger(logger, level => false);
            L10N = new L10N("better_portal");
            new TranslationsLoader(L10N).LoadTranslations(Path.Combine(ModLocation, "Languages"));

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), info.Metadata.GUID);
        }
    }
}