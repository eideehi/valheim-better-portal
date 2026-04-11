using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ModUtils;
using UnityEngine;

namespace BetterPortal
{
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [HarmonyPatch]
    internal static class Patches
    {
        private static bool _transpilerApplied;
        private static bool _findRandomOverrideLogged;

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(Game), nameof(Game.ConnectPortals))]
        private static IEnumerable<CodeInstruction> Game_ConnectPortals_Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var originalInstructions = instructions.ToList();
            var codeInstructions = originalInstructions
                .Select(instruction => new CodeInstruction(instruction))
                .ToList();

            var sourceTagField = AccessTools.Field(typeof(ZDOVars), "s_tag");
            var destTagField = AccessTools.Field(typeof(ZdoTags), nameof(ZdoTags.DestTag));
            var getStringWithDefault = AccessTools.Method(
                typeof(ZDO), nameof(ZDO.GetString), new[] { typeof(int), typeof(string) });
            var addToCurrentlyConnectingPortals = AccessTools.Method(
                typeof(Game), "AddToCurrentlyConnectingPortals");
            var setConnection = AccessTools.Method(
                typeof(Game), "SetConnection", new[] { typeof(ZDO), typeof(ZDOID), typeof(bool) });
            var uidField = AccessTools.Field(typeof(ZDO), nameof(ZDO.m_uid));

            var destTagReplacements = 0;
            for (var index = 0; index <= codeInstructions.Count - 4; index++)
            {
                if (!IsDestTagReadStore(codeInstructions, index, sourceTagField, getStringWithDefault))
                    continue;

                codeInstructions[index].operand = destTagField;
                destTagReplacements++;
            }

            var addToCurrentlyConnectingIndex = FindAddToCurrentlyConnectingPortalsIndex(
                codeInstructions, addToCurrentlyConnectingPortals);
            if (addToCurrentlyConnectingIndex >= 3)
                NopInstructions(codeInstructions, addToCurrentlyConnectingIndex - 3, 4);

            var reverseSetConnectionIndex = addToCurrentlyConnectingIndex >= 3
                ? FindReverseSetConnectionIndex(
                    codeInstructions, addToCurrentlyConnectingIndex, setConnection, uidField)
                : -1;
            if (reverseSetConnectionIndex >= 5)
                NopInstructions(codeInstructions, reverseSetConnectionIndex - 5, 6);

            if (destTagReplacements != 2 ||
                addToCurrentlyConnectingIndex < 3 ||
                reverseSetConnectionIndex < 5)
            {
                BetterPortal.Logger.Error(
                    $"Valheim 0.221.12 compatibility patch not applied completely. " +
                    $"DestTag replacements={destTagReplacements}/2, " +
                    $"AddToCurrentlyConnectingPortals={(addToCurrentlyConnectingIndex >= 0 ? "ok" : "missing")}, " +
                    $"Reverse SetConnection={(reverseSetConnectionIndex >= 0 ? "ok" : "missing")}.");
                return originalInstructions;
            }

            _transpilerApplied = true;
            BetterPortal.Logger.Info(
                "Applied Valheim 0.221.12 compatibility patch: DestTag replacements=2, " +
                "AddToCurrentlyConnectingPortals disabled=1, reverse SetConnection disabled=1.");
            return codeInstructions;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Game), "FindRandomUnconnectedPortal")]
        private static bool Game_FindRandomUnconnectedPortal_Prefix(ref ZDO __result,
            List<ZDO> portals, ZDO skip, string tag)
        {
            if (!_transpilerApplied)
                return true;

            if (!_findRandomOverrideLogged)
            {
                BetterPortal.Logger.Info("Applied BetterPortal FindRandomUnconnectedPortal override.");
                _findRandomOverrideLogged = true;
            }

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
                "[<color=yellow><b>@modifier_key + $KEY_Use</b></color>] @piece_portal_setdesttag");
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

            if (BetterPortal.IsModifierKeyPressed())
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
            if ((!__instance.m_inputField || !__instance.m_inputField.isFocused)) return;
            if (!TeleportWorldExtension.GetAllInstance()
                    .Any(x => ReferenceEquals(x, ___m_queuedSign))) return;

            TextInputExtension.Update(__instance);
        }

        private static bool IsDestTagReadStore(IReadOnlyList<CodeInstruction> instructions, int start,
            FieldInfo sourceTagField, MethodInfo getStringWithDefault)
        {
            return start + 3 < instructions.Count &&
                   instructions[start].opcode == OpCodes.Ldsfld &&
                   Equals(instructions[start].operand, sourceTagField) &&
                   instructions[start + 1].opcode == OpCodes.Ldstr &&
                   Equals(instructions[start + 1].operand, "") &&
                   instructions[start + 2].opcode == OpCodes.Callvirt &&
                   Equals(instructions[start + 2].operand, getStringWithDefault) &&
                   IsStoreLocal(instructions[start + 3].opcode);
        }

        private static int FindAddToCurrentlyConnectingPortalsIndex(
            IReadOnlyList<CodeInstruction> instructions, MethodInfo addToCurrentlyConnectingPortals)
        {
            for (var index = 3; index < instructions.Count; index++)
            {
                if (instructions[index].opcode != OpCodes.Call ||
                    !Equals(instructions[index].operand, addToCurrentlyConnectingPortals) ||
                    instructions[index - 3].opcode != OpCodes.Ldarg_0 ||
                    !IsLoadLocal(instructions[index - 2].opcode) ||
                    !IsLoadLocal(instructions[index - 1].opcode))
                    continue;

                return index;
            }

            return -1;
        }

        private static int FindReverseSetConnectionIndex(IReadOnlyList<CodeInstruction> instructions,
            int addToCurrentlyConnectingIndex, MethodInfo setConnection, FieldInfo uidField)
        {
            var foundAfterAddTo = 0;
            for (var index = addToCurrentlyConnectingIndex + 1; index < instructions.Count; index++)
            {
                if (index < 5 ||
                    instructions[index].opcode != OpCodes.Call ||
                    !Equals(instructions[index].operand, setConnection) ||
                    instructions[index - 5].opcode != OpCodes.Ldarg_0 ||
                    !IsLoadLocal(instructions[index - 4].opcode) ||
                    !IsLoadLocal(instructions[index - 3].opcode) ||
                    instructions[index - 2].opcode != OpCodes.Ldfld ||
                    !Equals(instructions[index - 2].operand, uidField) ||
                    instructions[index - 1].opcode != OpCodes.Ldc_I4_0)
                    continue;

                foundAfterAddTo++;
                if (foundAfterAddTo == 2)
                    return index;
            }

            return -1;
        }

        private static void NopInstructions(IList<CodeInstruction> instructions, int start, int length)
        {
            for (var index = start; index < start + length; index++)
                instructions[index] = Nop(instructions[index]);
        }

        private static CodeInstruction Nop(CodeInstruction instruction)
        {
            return new CodeInstruction(OpCodes.Nop)
            {
                labels = instruction.labels,
                blocks = instruction.blocks
            };
        }

        private static bool IsLoadLocal(OpCode opcode)
        {
            return opcode == OpCodes.Ldloc ||
                   opcode == OpCodes.Ldloc_S ||
                   opcode == OpCodes.Ldloc_0 ||
                   opcode == OpCodes.Ldloc_1 ||
                   opcode == OpCodes.Ldloc_2 ||
                   opcode == OpCodes.Ldloc_3;
        }

        private static bool IsStoreLocal(OpCode opcode)
        {
            return opcode == OpCodes.Stloc ||
                   opcode == OpCodes.Stloc_S ||
                   opcode == OpCodes.Stloc_0 ||
                   opcode == OpCodes.Stloc_1 ||
                   opcode == OpCodes.Stloc_2 ||
                   opcode == OpCodes.Stloc_3;
        }
    }
}
