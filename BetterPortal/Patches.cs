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
            var getConnectionZdoId = AccessTools.Method(
                typeof(ZDO), nameof(ZDO.GetConnectionZDOID),
                new[] { typeof(ZDOExtraData.ConnectionType) });
            var zdoIdNoneField = AccessTools.Field(typeof(ZDOID), nameof(ZDOID.None));
            var zdoIdEquality = AccessTools.Method(
                typeof(ZDOID), "op_Equality", new[] { typeof(ZDOID), typeof(ZDOID) });

            // Every match is located before anything is rewritten, so that an unexpected instruction
            // count aborts the whole patch instead of leaving Game.ConnectPortals half-modified.
            var destTagReadIndexes = FindDestTagReadStoreIndexes(
                codeInstructions, sourceTagField, getStringWithDefault);
            var addToIndexes = FindAddToCurrentlyConnectingPortalsIndexes(
                codeInstructions, addToCurrentlyConnectingPortals);

            var forwardSetConnectionIndexes = new List<int>();
            var reverseSetConnectionIndexes = new List<int>();
            if (addToIndexes.Count == 1)
                FindSetConnectionIndexes(codeInstructions, addToIndexes[0], setConnection, uidField,
                    forwardSetConnectionIndexes, reverseSetConnectionIndexes);

            var staleDestinationIndexes = FindStaleDestinationCheckIndexes(
                codeInstructions, getConnectionZdoId, zdoIdNoneField, zdoIdEquality);

            if (destTagReadIndexes.Count != 2 ||
                addToIndexes.Count != 1 ||
                forwardSetConnectionIndexes.Count != 1 ||
                reverseSetConnectionIndexes.Count != 1 ||
                staleDestinationIndexes.Count != 1)
            {
                BetterPortal.Logger.Error(
                    "Portal compatibility patch not applied: Game.ConnectPortals does not match what " +
                    "this build of BetterPortal expects, so vanilla portal behaviour is kept. " +
                    $"DestTag reads redirected={destTagReadIndexes.Count}/2, " +
                    $"AddToCurrentlyConnectingPortals disabled={addToIndexes.Count}/1, " +
                    $"forward SetConnection kept={forwardSetConnectionIndexes.Count}/1, " +
                    $"reverse SetConnection disabled={reverseSetConnectionIndexes.Count}/1, " +
                    $"stale destination check neutralized={staleDestinationIndexes.Count}/1.");
                // Harmony re-runs every transpiler from the original IL whenever the method is
                // re-patched, so a later failure has to clear the flag a previous success set:
                // the FindRandomUnconnectedPortal override is only correct alongside this rewrite.
                _transpilerApplied = false;
                return originalInstructions;
            }

            foreach (var index in destTagReadIndexes)
                codeInstructions[index].operand = destTagField;
            // m_currentlyConnectingPortals only exists so that the next pass force-applies a pending
            // connection in *both* directions (ClearCurrentlyConnectingPortals -> ForceSetConnection),
            // which would re-create exactly the reverse link this patch deliberately drops - so the
            // bookkeeping call has to go with it. What it covered was Game.SetConnection not always
            // writing locally: when a live peer owns the portal ZDO it only sends that peer an RPC. A
            // dropped RPC is now healed by the next 5-second pass instead, which is harmless because the
            // second loop keeps re-running for every portal that is still unconnected.
            NopInstructions(codeInstructions, addToIndexes[0] - 3, 4);
            NopInstructions(codeInstructions, reverseSetConnectionIndexes[0] - 5, 6);
            NeutralizeStaleDestinationCheck(codeInstructions, staleDestinationIndexes[0]);

            _transpilerApplied = true;
            BetterPortal.Logger.Info(
                "Applied portal compatibility patch: DestTag reads redirected=2, " +
                "AddToCurrentlyConnectingPortals disabled=1, reverse SetConnection disabled=1, " +
                "stale destination check neutralized=1.");
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

            var rawTag = zdo.GetString(ZDOVars.s_tag);
            var rawDest = zdo.GetString(ZdoTags.DestTag);

            // GetText() already runs the portal's own tag through the UGC filter. The destination tag is
            // stored raw and is only ever filtered for display, so it has to be filtered here.
            // Accepted limitation: vanilla filters its own tag with the author id it records alongside it
            // (ZDOVars.s_tagauthor), and filtering exempts text the local user wrote. The destination tag
            // is stored under the mod's own hash with no author, so it is filtered with a default
            // PlatformUserID - where filtering is active, a player's own destination tag can be censored
            // in the same hover line where their own tag is not. Fixing it means storing an author too.
            var tag = ToHoverTagText(__instance.GetText());
            var dest = ToHoverTagText(CensorShittyWords.FilterUGC(rawDest, UGCType.Text, 0L));

            var status = zdo.GetConnectionZDOID(ZDOExtraData.ConnectionType.Portal).IsNone()
                ? "$piece_portal_unconnected"
                : "$piece_portal_connected";

            var censored = CensorShittyWords.Filter(rawTag, out _) ||
                           CensorShittyWords.Filter(rawDest, out _);
            var censoredWarning = censored && ZNet.m_onlineBackend == OnlineBackendType.PlayFab
                ? "$piece_portal_censored_warning\n"
                : "";

            __result = BetterPortal.L10N.Localize(
                $"$piece_portal_tag:\"{tag}\"  @piece_portal_dest:\"{dest}\"  [{status}]\n" +
                censoredWarning +
                "[<color=yellow><b>$KEY_Use</b></color>] $piece_portal_settag\n" +
                "[<color=yellow><b>@modifier_key + $KEY_Use</b></color>] @piece_portal_setdesttag");
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.Interact))]
        private static bool TeleportWorld_Interact_Prefix(TeleportWorld __instance,
            ZNetView ___m_nview, ref bool __result, Humanoid human, bool hold, bool alt)
        {
            if (___m_nview.GetZDO() == null) return true;

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

            var modifierPressed = BetterPortal.IsModifierKeyPressed();
            var extension = __instance.GetComponent<TeleportWorldExtension>();
            // TextInput.RequestText assigns m_queuedSign before it calls sign.GetText(), so handing it a
            // missing receiver throws with the dialog already half-open. TeleportWorld_Awake_Postfix only
            // attaches the component when the portal has a ZDO, so fall back to vanilla instead: the
            // portal's own tag keeps working and only the destination tag is unavailable.
            if (modifierPressed && extension == null) return true;

            if (modifierPressed)
                TextInput.instance.RequestText(extension,
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
            var active = ___m_visibleFrame && !Console.IsVisible() && !Chat.instance.HasFocus() &&
                         __instance.m_inputField && __instance.m_inputField.isFocused &&
                         TeleportWorldExtension.GetAllInstance()
                             .Any(x => ReferenceEquals(x, ___m_queuedSign));
            if (!active)
            {
                PortalTagList.Hide();
                return;
            }

            // Update may rewrite the field text (Insert/Up/Down), and the list reads it afterwards.
            TextInputExtension.Update(__instance);
            PortalTagList.Update(__instance, (TeleportWorldExtension)___m_queuedSign);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ZInput), "Internal_GetMouseScrollWheel")]
        private static void ZInput_Internal_GetMouseScrollWheel_Postfix(ref float __result)
        {
            // GameCamera zooms on ZInput.GetMouseScrollWheel() without checking for an open text input,
            // so the wheel that scrolls the tag list would zoom the camera as well. The list itself is
            // scrolled by the UI input module and does not read ZInput. The public method is only a
            // tiny wrapper that the JIT may inline into its callers, out of reach of a patch, so the
            // private method behind it is patched instead; a postfix keeps the original's input-source
            // bookkeeping.
            if (PortalTagList.IsShown)
                __result = 0f;
        }

        /// <summary>
        /// Prepares an already UGC-filtered portal tag for the hover text: rich-text markup is stripped
        /// the way vanilla strips its own tag, and the mod's placeholder is substituted last, so that a
        /// tag which filtering or stripping reduces to nothing reads as empty instead of as a pair of
        /// empty quotes - and so that the placeholder itself is never filtered or stripped.
        /// </summary>
        private static string ToHoverTagText(string text)
        {
            var display = string.IsNullOrEmpty(text) ? "" : text.RemoveRichTextTags();
            return string.IsNullOrEmpty(display)
                ? BetterPortal.L10N.Translate("@empty_tag")
                : display;
        }

        /// <summary>
        /// Finds every "portal.GetString(ZDOVars.s_tag, "") -> local" read. Each one holds the tag that
        /// the following code matches other portals against, so redirecting them to the mod's own
        /// destination tag is what makes a portal point somewhere other than its own name.
        /// </summary>
        private static List<int> FindDestTagReadStoreIndexes(
            IReadOnlyList<CodeInstruction> instructions, FieldInfo sourceTagField,
            MethodInfo getStringWithDefault)
        {
            var indexes = new List<int>();
            for (var index = 0; index + 3 < instructions.Count; index++)
            {
                if (instructions[index].opcode != OpCodes.Ldsfld ||
                    !Equals(instructions[index].operand, sourceTagField) ||
                    instructions[index + 1].opcode != OpCodes.Ldstr ||
                    !Equals(instructions[index + 1].operand, "") ||
                    instructions[index + 2].opcode != OpCodes.Callvirt ||
                    !Equals(instructions[index + 2].operand, getStringWithDefault) ||
                    !IsStoreLocal(instructions[index + 3].opcode))
                    continue;

                indexes.Add(index);
            }

            return indexes;
        }

        private static List<int> FindAddToCurrentlyConnectingPortalsIndexes(
            IReadOnlyList<CodeInstruction> instructions, MethodInfo addToCurrentlyConnectingPortals)
        {
            var indexes = new List<int>();
            for (var index = 3; index < instructions.Count; index++)
            {
                if (instructions[index].opcode != OpCodes.Call ||
                    !Equals(instructions[index].operand, addToCurrentlyConnectingPortals) ||
                    instructions[index - 3].opcode != OpCodes.Ldarg_0 ||
                    !IsLoadLocal(instructions[index - 2].opcode) ||
                    !IsLoadLocal(instructions[index - 1].opcode))
                    continue;

                indexes.Add(index);
            }

            return indexes;
        }

        /// <summary>
        /// Splits the "SetConnection(source, target.m_uid)" / "SetConnection(target, source.m_uid)" pair
        /// that follows AddToCurrentlyConnectingPortals(source, target). The two calls are told apart by
        /// the order in which they load the two locals, so the reverse call - the one BetterPortal drops
        /// to keep connections one-directional - is identified structurally instead of by being "the
        /// second match".
        /// </summary>
        private static void FindSetConnectionIndexes(IReadOnlyList<CodeInstruction> instructions,
            int addToCurrentlyConnectingIndex, MethodInfo setConnection, FieldInfo uidField,
            ICollection<int> forwardIndexes, ICollection<int> reverseIndexes)
        {
            var sourceLocal = GetLocalIndex(instructions[addToCurrentlyConnectingIndex - 2]);
            var targetLocal = GetLocalIndex(instructions[addToCurrentlyConnectingIndex - 1]);
            if (sourceLocal < 0 || targetLocal < 0 || sourceLocal == targetLocal) return;

            for (var index = addToCurrentlyConnectingIndex + 1; index < instructions.Count; index++)
            {
                if (index < 5 ||
                    instructions[index].opcode != OpCodes.Call ||
                    !Equals(instructions[index].operand, setConnection) ||
                    instructions[index - 5].opcode != OpCodes.Ldarg_0 ||
                    instructions[index - 2].opcode != OpCodes.Ldfld ||
                    !Equals(instructions[index - 2].operand, uidField) ||
                    instructions[index - 1].opcode != OpCodes.Ldc_I4_0)
                    continue;

                var portalLocal = GetLocalIndex(instructions[index - 4]);
                var connectionLocal = GetLocalIndex(instructions[index - 3]);
                if (portalLocal == sourceLocal && connectionLocal == targetLocal)
                    forwardIndexes.Add(index);
                else if (portalLocal == targetLocal && connectionLocal == sourceLocal)
                    reverseIndexes.Add(index);
            }
        }

        /// <summary>
        /// Finds the clause Valheim 1.0 added to the first ConnectPortals loop, which also tears a
        /// connection down when the destination portal holds no connection of its own:
        /// <c>ldloc destination / ldc.i4.1 / callvirt ZDO::GetConnectionZDOID / ldsfld ZDOID::None /
        /// call ZDOID::op_Equality / brfalse keepConnection</c>.
        /// </summary>
        private static List<int> FindStaleDestinationCheckIndexes(
            IReadOnlyList<CodeInstruction> instructions, MethodInfo getConnectionZdoId,
            FieldInfo zdoIdNoneField, MethodInfo zdoIdEquality)
        {
            var indexes = new List<int>();
            for (var index = 0; index + 5 < instructions.Count; index++)
            {
                if (!IsLoadLocal(instructions[index].opcode) ||
                    !IsPortalConnectionType(instructions[index + 1]) ||
                    instructions[index + 2].opcode != OpCodes.Callvirt ||
                    !Equals(instructions[index + 2].operand, getConnectionZdoId) ||
                    instructions[index + 3].opcode != OpCodes.Ldsfld ||
                    !Equals(instructions[index + 3].operand, zdoIdNoneField) ||
                    instructions[index + 4].opcode != OpCodes.Call ||
                    !Equals(instructions[index + 4].operand, zdoIdEquality) ||
                    !IsBranchIfFalse(instructions[index + 5].opcode))
                    continue;

                indexes.Add(index);
            }

            return indexes;
        }

        /// <summary>
        /// Turns the stale-destination clause into an unconditional jump to its "keep the connection"
        /// target. BetterPortal deliberately connects portals one way only, so a portal that is purely a
        /// destination has no connection of its own and vanilla would disconnect it on every pass.
        /// The comparison is replaced by Nop rather than deleted so that the method stays
        /// stack-balanced: the clause begins with an empty evaluation stack, five Nops push nothing and
        /// the unconditional branch consumes nothing. Nop'ing the branch as well would instead fall
        /// through into the disconnect block. The earlier "destination is missing" and "tags do not
        /// match" branches still reach that block, so genuinely stale connections keep being cleared.
        /// </summary>
        private static void NeutralizeStaleDestinationCheck(IList<CodeInstruction> instructions,
            int start)
        {
            NopInstructions(instructions, start, 5);
            instructions[start + 5].opcode = OpCodes.Br;
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

        private static bool IsBranchIfFalse(OpCode opcode)
        {
            return opcode == OpCodes.Brfalse || opcode == OpCodes.Brfalse_S;
        }

        private static bool IsPortalConnectionType(CodeInstruction instruction)
        {
            var opcode = instruction.opcode;
            if (opcode == OpCodes.Ldc_I4_1)
                return (int)ZDOExtraData.ConnectionType.Portal == 1;
            if (opcode != OpCodes.Ldc_I4 && opcode != OpCodes.Ldc_I4_S)
                return false;

            var operand = instruction.operand;
            if (operand is int)
                return (int)operand == (int)ZDOExtraData.ConnectionType.Portal;
            if (operand is sbyte)
                return (sbyte)operand == (int)ZDOExtraData.ConnectionType.Portal;
            if (operand is byte)
                return (byte)operand == (int)ZDOExtraData.ConnectionType.Portal;
            return false;
        }

        /// <summary>
        /// Returns the index of the local a load-local instruction reads, or -1 when the instruction is
        /// not a local load. Harmony keeps the compact forms as distinct opcodes and hands the explicit
        /// forms a LocalVariableInfo (or a raw index) as their operand, so both shapes are handled.
        /// </summary>
        private static int GetLocalIndex(CodeInstruction instruction)
        {
            var opcode = instruction.opcode;
            if (opcode == OpCodes.Ldloc_0) return 0;
            if (opcode == OpCodes.Ldloc_1) return 1;
            if (opcode == OpCodes.Ldloc_2) return 2;
            if (opcode == OpCodes.Ldloc_3) return 3;
            if (opcode != OpCodes.Ldloc && opcode != OpCodes.Ldloc_S) return -1;

            var operand = instruction.operand;
            var local = operand as LocalVariableInfo;
            if (local != null) return local.LocalIndex;
            if (operand is int) return (int)operand;
            if (operand is short) return (short)operand;
            if (operand is byte) return (byte)operand;
            return -1;
        }
    }
}
