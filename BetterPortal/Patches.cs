using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace BetterPortal
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    [HarmonyPatch(typeof(Game))]
    internal static class GameCoroutinePatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            var type = AccessTools.FirstInner(typeof(Game),
                _type => _type.Name.Contains("ConnectPortals"));
            return AccessTools.FirstMethod(type, method => method.Name.Contains("MoveNext"));
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> MoveNext_Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions)
                .MatchStartForward(
                    new CodeMatch(OpCodes.Ldstr, "tag"),
                    new CodeMatch(OpCodes.Ldstr, ""),
                    new CodeMatch(OpCodes.Callvirt,
                        AccessTools.Method(typeof(ZDO), "GetString",
                            new[] { typeof(string), typeof(string) })),
                    new CodeMatch(OpCodes.Stloc_S))
                .Repeat(matcher =>
                    matcher.SetInstruction(
                        new CodeInstruction(OpCodes.Ldstr, "desttag")))
                .End()
                .MatchStartBackwards(
                    new CodeMatch(OpCodes.Ldloc_S),
                    new CodeMatch(OpCodes.Ldstr, "target"),
                    new CodeMatch(OpCodes.Ldloc_S),
                    new CodeMatch(OpCodes.Ldfld,
                        AccessTools.Field(typeof(ZDO), "m_uid")),
                    new CodeMatch(OpCodes.Callvirt,
                        AccessTools.Method(typeof(ZDO), "Set",
                            new[] { typeof(string), typeof(ZDOID) })))
                .RemoveInstructions(5)
                .InstructionEnumeration();
        }
    }

    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [HarmonyPatch]
    internal static class Patches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Game), "FindRandomUnconnectedPortal")]
        private static bool Game_FindRandomUnconnectedPortal_Prefix(ref ZDO __result,
            List<ZDO> portals, ZDO skip, string tag)
        {
            var list = portals
                .Where(portal => portal != skip && portal.GetString("tag") == tag)
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
            if (___m_nview.GetZDO() == null) return true;

            var tag = __instance.GetText();
            if (string.IsNullOrEmpty(tag))
                tag = BetterPortal.L10N.Translate("@empty_tag");

            var dest = ___m_nview.GetZDO().GetString("desttag");
            if (string.IsNullOrEmpty(dest))
                dest = BetterPortal.L10N.Translate("@empty_tag");

            var status = ___m_nview.GetZDO().GetZDOID("target") != ZDOID.None
                ? "$piece_portal_connected"
                : "$piece_portal_unconnected";
            var info = $"$piece_portal_tag:\"{tag}\"  @piece_portal_dest:\"{dest}\"  [{status}]";
            var controls = "[<color=yellow><b>$KEY_Use</b></color>] $piece_portal_settag\n" +
                           "[<color=yellow><b>@shift_key + $KEY_Use</b></color>] @piece_portal_setdesttag";

            __result = BetterPortal.L10N.Localize(info + "\n" + controls);
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
    }
}