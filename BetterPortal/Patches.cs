using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace BetterPortal
{
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [HarmonyPatch]
    internal static class Patches
    {
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(Game), nameof(Game.ConnectPortals))]
        private static IEnumerable<CodeInstruction> Game_ConnectPortals_Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            /*
             *   ZDOID connectionZdoid = zdo1.GetConnectionZDOID(ZDOExtraData.ConnectionType.Portal);
             * - string str = zdo1.GetString(ZDOVars.s_tag);
             * + string str = zdo1.GetString(ZdoTags.DestTag);
             * ...
             * - string tag = skip.GetString(ZDOVars.s_tag);
             * + string tag = skip.GetString(ZdoTags.DestTag);
             *   ZDO unconnectedPortal = this.FindRandomUnconnectedPortal(portals, skip, tag);
             */
            var codeInstructions = new List<CodeInstruction>(instructions);
            codeInstructions.ForEach(x => BetterPortal.Logger.Message(x.ToString()));
            codeInstructions = new CodeMatcher(codeInstructions)
                .MatchStartForward(
                    new CodeMatch(OpCodes.Ldsfld, AccessTools.Field(typeof(ZDOVars), "s_tag")),
                    new CodeMatch(OpCodes.Ldstr, ""),
                    new CodeMatch(OpCodes.Callvirt,
                        AccessTools.Method(typeof(ZDO), "GetString",
                            new[] { typeof(int), typeof(string) })),
                    new CodeMatch(OpCodes.Stloc_S))
                .Repeat(matcher =>
                    matcher.SetInstruction(
                        new CodeInstruction(OpCodes.Ldsfld,
                            AccessTools.Field(typeof(ZdoTags), "DestTag"))))
                .Instructions();
            codeInstructions.ForEach(x => BetterPortal.Logger.Debug(x.ToString()));
            return codeInstructions;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Game), "FindRandomUnconnectedPortal")]
        private static bool Game_FindRandomUnconnectedPortal_Prefix(ref ZDO __result,
            List<ZDO> portals, ZDO skip, string tag)
        {
            var list = portals
                .Where(portal => portal != skip && portal.GetString(ZDOVars.s_tag) == tag)
                .ToList();
            __result = list.Count == 0 ? null : list[Random.Range(0, list.Count)];
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TeleportWorld), "Awake")]
        private static void TeleportWorld_Awake_Postfix(TeleportWorld __instance,
            ZNetView ___m_nview)
        {
            if (___m_nview.GetZDO() != null)
                __instance.gameObject.AddComponent<TeleportWorldExtension>();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.GetHoverText))]
        private static bool TeleportWorld_GetHoverText_Prefix(TeleportWorld __instance,
            ZNetView ___m_nview, ref string __result)
        {
            var zdo = ___m_nview.GetZDO();
            if (zdo == null) return true;

            var tag = __instance.GetText();
            if (string.IsNullOrEmpty(tag))
                tag = BetterPortal.L10N.Translate("@empty_tag");

            var dest = zdo.GetString(ZdoTags.DestTag);
            if (string.IsNullOrEmpty(dest))
                dest = BetterPortal.L10N.Translate("@empty_tag");

            var status = zdo.GetConnectionZDOID(ZDOExtraData.ConnectionType.Portal).IsNone()
                ? "$piece_portal_unconnected"
                : "$piece_portal_connected";

            __result = BetterPortal.L10N.Localize(
                $"$piece_portal_tag:\"{tag}\"  @piece_portal_dest:\"{dest}\"  [{status}]\n" +
                "[<color=yellow><b>$KEY_Use</b></color>] $piece_portal_settag\n" +
                "[<color=yellow><b>@shift_key + $KEY_Use</b></color>] @piece_portal_setdesttag");
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.Interact))]
        private static bool TeleportWorld_Interact_Prefix(TeleportWorld __instance,
            ZNetView ___m_nview, ref bool __result, Humanoid human, bool hold, bool alt)
        {
            if (hold)
            {
                __result = false;
                return false;
            }

            if (!PrivateArea.CheckAccess(__instance.transform.position))
            {
                human.Message(MessageHud.MessageType.Center, "$piece_noaccess");
                __result = true;
                return false;
            }

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                TextInput.instance.RequestText(__instance.GetComponent<TeleportWorldExtension>(),
                    BetterPortal.L10N.Translate("@piece_portal_dest"), 10);
            else
                TextInput.instance.RequestText(__instance, "$piece_portal_tag", 10);

            __result = true;
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TextInput), "Update")]
        private static void TextInput_Update_Postfix(TextInput __instance,
            TextReceiver ___m_queuedSign, bool ___m_visibleFrame)
        {
            if (!___m_visibleFrame || Console.IsVisible() || Chat.instance.HasFocus()) return;
            if ((!__instance.m_textField || !__instance.m_textField.isFocused) &&
                (!__instance.m_textFieldTMP || !__instance.m_textFieldTMP.isFocused)) return;
            if (!TeleportWorldExtension.GetAllInstance()
                    .Any(x => ReferenceEquals(x, ___m_queuedSign))) return;

            TextInputExtension.Update(__instance);
        }
    }
}