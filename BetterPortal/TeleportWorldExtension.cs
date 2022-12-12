using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BetterPortal
{
    [DisallowMultipleComponent]
    internal class TeleportWorldExtension : MonoBehaviour, TextReceiver
    {
        private ZNetView _zNetView;

        private void Awake()
        {
            _zNetView = GetComponent<ZNetView>();
            _zNetView.Register<string>("SetTagDest", RPC_SetTagDest);
        }

        private void OnDestroy()
        {
            _zNetView = null;
        }

        private IEnumerator UpdateConnection()
        {
            var zdo = _zNetView.GetZDO();
            var dest = zdo.GetString("desttag");

            var targetId = zdo.GetZDOID("target");
            if (!targetId.IsNone())
            {
                var target = ZDOMan.instance.GetZDO(targetId);
                if (target == null || target.GetString("tag") != dest)
                {
                    zdo.SetOwner(ZDOMan.instance.GetMyID());
                    zdo.Set("target", ZDOID.None);
                    ZDOMan.instance.ForceSendZDO(zdo.m_uid);
                }
            }

            if (!zdo.GetZDOID("target").IsNone())
            {
                StopCoroutine(nameof(UpdateConnection));
                yield return null;
            }

            var portals = new List<ZDO>();
            var index = 0;
            bool done;
            do
            {
                done = ZDOMan.instance.GetAllZDOsWithPrefabIterative(
                    Game.instance.m_portalPrefab.name, portals, ref index);
                yield return null;
            } while (!done);

            portals = portals
                .Where(x => x != zdo && x.GetString("tag") == dest)
                .ToList();
            var portal = portals.Count == 0
                ? null
                : portals[Random.Range(0, portals.Count)];
            if (portal != null)
            {
                zdo.SetOwner(ZDOMan.instance.GetMyID());
                zdo.Set("target", portal.m_uid);
                ZDOMan.instance.ForceSendZDO(zdo.m_uid);
            }

            StopCoroutine(nameof(UpdateConnection));
            yield return null;
        }

        public string GetText()
        {
            return _zNetView.GetZDO().GetString("desttag");
        }

        public void SetText(string text)
        {
            if (_zNetView.IsValid())
                _zNetView.InvokeRPC("SetTagDest", text);
        }

        private void RPC_SetTagDest(long sender, string tagDest)
        {
            if (!_zNetView.IsValid() || !_zNetView.IsOwner() || GetText() == tagDest) return;

            _zNetView.GetZDO().Set("desttag", tagDest);
            StartCoroutine(nameof(UpdateConnection));
        }
    }
}