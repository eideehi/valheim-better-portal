using System;
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
    internal static class GameInnerClassPatch
    {
        private static MethodBase TargetMethod()
        {
            var type = AccessTools.FirstInner(typeof(Game), _type =>
            {
                BetterPortal.ModLogger.LogDebug(_type.Name);
                return _type.Name.Contains("ConnectPortals");
            });
            return AccessTools.FirstMethod(type, method => method.Name.Contains("MoveNext"));
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> MoveNext(IEnumerable<CodeInstruction> instructions)
        {
            var GetString =
                AccessTools.Method(typeof(ZDO), "GetString", new[] { typeof(string), typeof(string) });

            var codes = new List<CodeInstruction>(instructions);
            var i = 0;
            do
            {
                if (codes[i].opcode != OpCodes.Ldstr || !(codes[i].operand is string str) || str != "tag")
                {
                    continue;
                }

                var j = i;
                while (++j < codes.Count)
                {
                    if (!codes[j].Calls(GetString))
                    {
                        continue;
                    }

                    if (codes[j + 1].opcode == OpCodes.Stloc_S)
                    {
                        codes.RemoveAt(i);
                        codes.Insert(i, new CodeInstruction(OpCodes.Ldstr, "desttag"));
                    }

                    break;
                }
            } while (++i < codes.Count);

            return codes.AsEnumerable();
        }
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [HarmonyPatch(typeof(Game))]
    internal static class GamePatch
    {
        [SuppressMessage("ReSharper", "RedundantAssignment")]
        [HarmonyPrefix, HarmonyPatch("FindRandomUnconnectedPortal")]
        private static bool FindRandomUnconnectedPortal(ref ZDO __result, List<ZDO> portals, ZDO skip, string tag)
        {
            var list = portals.Where(portal => portal != skip && portal.GetString(nameof(tag)) == tag).ToList();
            __result = list.Count == 0 ? null : list[UnityEngine.Random.Range(0, list.Count)];
            return false;
        }
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [HarmonyPatch(typeof(TeleportWorld))]
    internal static class TeleportWorldPatch
    {
        [HarmonyReversePatch, HarmonyPatch("HaveTarget")]
        private static bool HaveTarget(object instance)
        {
            throw new NotImplementedException("It's a stub");
        }

        [HarmonyPostfix, HarmonyPatch("Awake")]
        private static void Awake(TeleportWorld __instance, ZNetView ___m_nview)
        {
            if (___m_nview.GetZDO() != null)
            {
                __instance.gameObject.AddComponent<TeleportExtraData>();
            }
        }

        [HarmonyPrefix, HarmonyPatch(nameof(TeleportWorld.GetHoverText))]
        private static bool GetHoverText(TeleportWorld __instance, ZNetView ___m_nview, ref string __result)
        {
            if (___m_nview.GetZDO() == null)
            {
                return true;
            }

            var tag = __instance.GetText();
            if (string.IsNullOrEmpty(tag))
            {
                tag = L10N.Translate("@empty_tag");
            }

            var dest = ___m_nview.GetZDO().GetString("desttag");
            if (string.IsNullOrEmpty(dest))
            {
                dest = L10N.Translate("@empty_tag");
            }

            var status = (HaveTarget(__instance) ? "$piece_portal_connected" : "$piece_portal_unconnected");
            var info = "$piece_portal_tag:\"" + tag + "\"  @piece_portal_dest:\"" + dest + "\" [" + status + "]";
            var desc = "[<color=yellow><b>$KEY_Use</b></color>] $piece_portal_settag\n" +
                       "[<color=yellow><b>@shift_key + $KEY_Use</b></color>] @piece_portal_setdesttag";

            __result = L10N.Localize(info + "\n" + desc);
            return false;
        }

        [SuppressMessage("ReSharper", "RedundantAssignment")]
        [HarmonyPrefix, HarmonyPatch(nameof(TeleportWorld.Interact))]
        private static bool Interact(TeleportWorld __instance, ZNetView ___m_nview, ref bool __result, Humanoid human,
            bool hold, bool alt)
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
            {
                TextInput.instance.RequestText(__instance.GetComponent<TeleportExtraData>(),
                    L10N.Translate("@piece_portal_dest"), 10);
            }
            else
            {
                TextInput.instance.RequestText(__instance, "$piece_portal_tag", 10);
            }

            __result = true;
            return false;
        }
    }

    [DisallowMultipleComponent]
    internal class TeleportExtraData : MonoBehaviour, TextReceiver
    {
        private ZNetView _zNetView;

        private void Awake()
        {
            _zNetView = GetComponent<ZNetView>();
            _zNetView.Register<string>("SetTagDest", RPC_SetTagDest);
        }

        public string GetText()
        {
            return _zNetView.GetZDO().GetString("desttag");
        }

        public void SetText(string text)
        {
            if (_zNetView.IsValid())
            {
                _zNetView.InvokeRPC("SetTagDest", text);
            }
        }

        private void RPC_SetTagDest(long sender, string tagDest)
        {
            if (_zNetView.IsValid() && _zNetView.IsOwner() && GetText() != tagDest)
            {
                _zNetView.GetZDO().Set("desttag", tagDest);
            }
        }
    }
}