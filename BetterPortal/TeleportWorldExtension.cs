using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BetterPortal
{
    [DisallowMultipleComponent]
    internal class TeleportWorldExtension : MonoBehaviour, TextReceiver
    {
        private static readonly List<TeleportWorldExtension> AllInstance;

        static TeleportWorldExtension()
        {
            AllInstance = new List<TeleportWorldExtension>();
        }

        private ZNetView _zNetView;

        public static IEnumerable<TeleportWorldExtension> GetAllInstance()
        {
            return AllInstance;
        }

        private void Awake()
        {
            _zNetView = GetComponent<ZNetView>();
            _zNetView.Register<string>("SetTagDest", RPC_SetTagDest);

            AllInstance.Add(this);
        }

        private void OnDestroy()
        {
            AllInstance.Remove(this);

            _zNetView = null;
        }

        private IEnumerator UpdateConnection()
        {
            var zdo = _zNetView.GetZDO();
            var dest = zdo.GetString(ZdoTags.DestTag);

            var targetId = zdo.GetConnectionZDOID(ZDOExtraData.ConnectionType.Portal);
            if (!targetId.IsNone())
            {
                var target = ZDOMan.instance.GetZDO(targetId);
                if (target == null || target.GetString(ZDOVars.s_tag) != dest)
                {
                    zdo.SetOwner(ZDOMan.GetSessionID());
                    zdo.UpdateConnection(ZDOExtraData.ConnectionType.Portal, ZDOID.None);
                    ZDOMan.instance.ForceSendZDO(zdo.m_uid);
                }
            }

            if (!zdo.GetConnectionZDOID(ZDOExtraData.ConnectionType.Portal).IsNone())
            {
                StopCoroutine(nameof(UpdateConnection));
                yield return null;
            }

            var portals = ZDOMan.instance.GetPortals()
                .Where(x => x != zdo && x.GetString(ZDOVars.s_tag) == dest)
                .ToList();
            var portal = portals.Count == 0
                ? null
                : portals[Random.Range(0, portals.Count)];
            if (portal != null)
            {
                zdo.SetOwner(ZDOMan.GetSessionID());
                zdo.UpdateConnection(ZDOExtraData.ConnectionType.Portal, portal.m_uid);
                ZDOMan.instance.ForceSendZDO(zdo.m_uid);
            }

            StopCoroutine(nameof(UpdateConnection));
            yield return null;
        }

        public string GetText()
        {
            return _zNetView.GetZDO().GetString(ZdoTags.DestTag);
        }

        public void SetText(string text)
        {
            if (_zNetView.IsValid())
                _zNetView.InvokeRPC("SetTagDest", text);
        }

        private void RPC_SetTagDest(long sender, string tagDest)
        {
            if (!_zNetView.IsValid() || !_zNetView.IsOwner() || GetText() == tagDest) return;

            _zNetView.GetZDO().Set(ZdoTags.DestTag, tagDest);
            StartCoroutine(nameof(UpdateConnection));
        }
    }
}