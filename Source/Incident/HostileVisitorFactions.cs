using System.Linq;
using RimWorld;
using Verse;

namespace SR.ModRimWorld.RaidExtension
{
    public static class HostileVisitorFactions
    {
        public static bool MayWalkInOn(Faction f, Map map)
        {
            var layer = map.Tile.LayerDef;
            var arrival = f.def;
            if (!arrival.arrivalLayerWhitelist.NullOrEmpty() && !arrival.arrivalLayerWhitelist.Contains(layer))
            {
                return false;
            }

            if (!arrival.arrivalLayerBlacklist.NullOrEmpty() && arrival.arrivalLayerBlacklist.Contains(layer))
            {
                return false;
            }

            if (layer.onlyAllowWhitelistedArrivals && arrival.arrivalLayerWhitelist.NullOrEmpty())
            {
                return false;
            }

            var parms = new IncidentParms { target = map, faction = f };
            return PawnsArrivalModeDefOf.EdgeWalkIn.Worker.CanUseWith(parms);
        }
    }
}
