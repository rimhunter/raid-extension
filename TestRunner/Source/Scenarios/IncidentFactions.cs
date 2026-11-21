using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class IncidentFactions
    {
        [Scenario("poaching-and-logging-pick-only-visible-npc-factions-among-30", MapSize = 100, MaxSeconds = 150)]
        static void OnlyVisibleNpcFactions(ScenarioContext s) {
            IncidentHelpers.AddVisibleFactions(30);
            s.AreEqual(30, Find.FactionManager.AllFactionsVisible.Count(), "the world should hold 30 visible factions");
            var hiddenHostile = Find.FactionManager.AllFactions.Where(f => f.Hidden && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer)).Select(f => f.Name).ToList();
            s.Check(hiddenHostile.Count > 0, "the world should hold a hidden, humanlike, hostile faction for the check to be able to fail");

            IncidentHelpers.SpawnWildAnimal(s, "Muffalo", s.Cell(0, 0));
            for (var i = 0; i < 4; i++) IncidentHelpers.SpawnTree(s, s.Cell(-6 + 3 * i, 4));
            var logBefore = IncidentHelpers.ModLogCount();

            var seen = new List<string>();
            var unfired = new List<string>();
            foreach (var incident in new[] { "SrPoaching", "SrLogging" }) {
                for (var i = 0; i < 10; i++) {
                    var run = IncidentHelpers.Fire(s, incident, null, 1500f);
                    if (!run.Fired) unfired.Add(incident + " " + i + ": " + string.Join(" | ", run.LogLines));
                    foreach (var faction in run.Pawns.Select(p => p.Faction).Distinct()) {
                        seen.Add(incident + ":" + faction.Name);
                        s.Check(Find.FactionManager.AllFactions.Contains(faction), incident + " picked " + faction.Name + ", which is not a faction of the world");
                        s.Check(!faction.Hidden, incident + " picked the hidden faction " + faction.Name + " (" + faction.def.defName + ")");
                        s.Check(!faction.IsPlayer, incident + " picked the player's faction");
                        s.Check(faction.HostileTo(Faction.OfPlayer), incident + " picked " + faction.Name + ", which is not hostile to the player");
                    }
                    foreach (var pawn in run.Pawns.ToList()) pawn.Destroy();
                    foreach (var lord in s.Map.lordManager.lords.ToList()) s.Map.lordManager.RemoveLord(lord);
                }
            }
            s.AreEqual("", string.Join(" || ", unfired), "the firings that did not fire");
            s.AreEqual(20, seen.Count, "every firing should have put one faction on the map; seen: " + string.Join(", ", seen));
            IncidentHelpers.CheckModLogQuiet(s, logBefore, "across 20 firings");
            s.RequireNoLogErrors();
        }
    }
}
