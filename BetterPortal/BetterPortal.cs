using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace BetterPortal
{
    [BepInPlugin(ModId, ModName, ModVersion)]
    public class BetterPortal : BaseUnityPlugin
    {
        private const string ModId = "net.eidee.valheim.better_portal";
        private const string ModName = "Better Portal";
        private const string ModVersion = "1.0.0";

        internal static string ModLocation { get; private set; }
        internal static ManualLogSource ModLogger { get; private set; }
        internal static ConfigFile ModConfig { get; private set; }

        private void Awake()
        {
            ModLocation = Path.GetDirectoryName(Info.Location) ?? "";
            ModLogger = Logger;
            ModConfig = Config;

            TranslationsLoader.LoadFromJson(Path.Combine(ModLocation, "Languages"));

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), ModId);
        }
    }
}