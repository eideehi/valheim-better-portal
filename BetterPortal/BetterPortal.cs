using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ModUtils;
using UnityEngine;

namespace BetterPortal
{
    [BepInPlugin(ModId, ModName, ModVersion)]
    public class UnityPlugin : BaseUnityPlugin
    {
        private const string ModId = "net.eidee.valheim.better_portal";
        private const string ModName = "Better Portal";
        private const string ModVersion = "1.0.7";

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
        public static ConfigEntry<KeyCode> ModifierKey { get; private set; }

        public static void Initialize(PluginInfo info, ManualLogSource logger, ConfigFile config)
        {
            ModLocation = Path.GetDirectoryName(info.Location) ?? "";
            Logger = new ModUtils.Logger(logger,
                level => level != LogLevel.Debug && level != LogLevel.Message);
            L10N = new L10N("better_portal");
            L10N.AddTranslationDirectory(Path.Combine(ModLocation, "Languages"));

            var configuration = new Configuration(config, L10N);
            ModifierKey = configuration.Bind("general", "ModifierKey", KeyCode.LeftShift);
            ValidateModifierKey();
            UpdateModifierKeyDisplay(ModifierKey.Value);
            ModifierKey.SettingChanged += (sender, args) =>
            {
                ValidateModifierKey();
                UpdateModifierKeyDisplay(ModifierKey.Value);
            };

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), info.Metadata.GUID);
        }

        public static bool IsModifierKeyPressed()
        {
            var key = ModifierKey.Value;
            var paired = GetPairedKey(key);
            return Input.GetKey(key) || (paired != KeyCode.None && Input.GetKey(paired));
        }

        private static void ValidateModifierKey()
        {
            if (ModifierKey.Value == KeyCode.None)
                ModifierKey.Value = (KeyCode)ModifierKey.DefaultValue;
        }

        private static void UpdateModifierKeyDisplay(KeyCode keyCode)
        {
            L10N.AddWord("@modifier_key", GetKeyDisplayName(keyCode));
        }

        private static KeyCode GetPairedKey(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftShift:    return KeyCode.RightShift;
                case KeyCode.RightShift:   return KeyCode.LeftShift;
                case KeyCode.LeftControl:  return KeyCode.RightControl;
                case KeyCode.RightControl: return KeyCode.LeftControl;
                case KeyCode.LeftAlt:      return KeyCode.RightAlt;
                case KeyCode.RightAlt:     return KeyCode.LeftAlt;
                default:                   return KeyCode.None;
            }
        }

        private static string GetKeyDisplayName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftShift:
                case KeyCode.RightShift:   return "Shift";
                case KeyCode.LeftControl:
                case KeyCode.RightControl: return "Ctrl";
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:     return "Alt";
                default:                   return key.ToString();
            }
        }
    }
}
