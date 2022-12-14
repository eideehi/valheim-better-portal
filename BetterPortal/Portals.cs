using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace BetterPortal
{
    internal static class Portals
    {
        private static readonly List<ZDO> Cache;

        static Portals()
        {
            Cache = new List<ZDO>();
        }

        [UsedImplicitly]
        public static void Update(IEnumerable<ZDO> portals)
        {
            Cache.Clear();
            Cache.AddRange(portals);
        }

        public static IEnumerable<ZDO> GetAll()
        {
            return Cache.ToList();
        }
    }
}