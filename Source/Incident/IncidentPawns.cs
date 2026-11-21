using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SR.ModRimWorld.RaidExtension
{
    public static class IncidentPawns
    {
        public static void InitializeWorkSettings(IEnumerable<Pawn> pawns)
        {
            foreach (var pawn in pawns)
            {
                if (!pawn.RaceProps.Humanlike)
                {
                    continue;
                }

                if (pawn.workSettings == null)
                {
                    throw new InvalidOperationException(
                        $"Raid Extension: humanlike pawn {pawn.ThingID} has no work settings object");
                }

                pawn.workSettings.EnableAndInitializeIfNotAlreadyInitialized();
            }
        }
    }
}
