using System.Linq;
using RimWorld;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class Poaching
    {
        [Scenario("poaching-hostile-group-hunts-a-large-animal-and-leaves", MapSize = 100, MaxSeconds = 150)]
        static void PoachersHuntAndLeave(ScenarioContext s) {
            var pirates = IncidentHelpers.FactionOf("Pirate");
            var animal = IncidentHelpers.SpawnWildAnimal(s, "Muffalo", s.Cell(0, 0));
            var logBefore = IncidentHelpers.ModLogCount();
            var run = IncidentHelpers.Fire(s, "SrPoaching", pirates, 1500f);
            IncidentHelpers.CheckGroup(s, run, "SrPoaching", pirates);
            IncidentHelpers.CheckLetter(s, run, "SrPoaching", pirates);

            var lords = IncidentHelpers.LordJobs(s.Map);
            s.AreEqual(1, lords.Count, "one lord should run the group; lords: " + string.Join(", ", lords));
            s.Check(lords.Any(l => l.StartsWith("LordJobPoaching/")), "the group should run the poaching lord job; lords: " + string.Join(", ", lords));

            var messagesBefore = IncidentHelpers.MessageCount();
            var hunting = s.RunUntil(() => run.Pawns.Any(p => IncidentHelpers.DutyOf(p) == "SrPoaching"), 3000);
            s.Check(hunting, "the poachers should start the Poaching duty");
            s.Check(!animal.Dead, "the large animal should be alive before the hunt");
            var killed = s.RunUntil(() => animal.Dead, 6000);
            s.Check(killed, "the poachers should kill the large animal");
            var gone = s.RunUntil(() => run.Pawns.All(p => !p.Spawned), 12000);
            s.Check(gone, "the poachers should leave the map; still on the map: " + string.Join(", ", run.Pawns.Where(p => p.Spawned).Select(p => p.LabelShort + " duty=" + IncidentHelpers.DutyOf(p))));
            IncidentHelpers.CheckLeaveMessage(s, IncidentHelpers.NewMessages(messagesBefore), "SrTakePreyExit", pirates);
            s.AreEqual(0, s.Map.listerThings.AllThings.Count(t => t is Corpse c && c.InnerPawn == animal), "the poachers should take the dead prey's corpse off the map");
            IncidentHelpers.CheckModLogQuiet(s, logBefore, "during a poaching run");
            s.RequireNoLogErrors();
            s.RequireNoPlayerLogErrors();
        }

        [Scenario("poaching-group-size-grows-with-the-points", MapSize = 100, MaxSeconds = 90)]
        static void PoachingPointsScale(ScenarioContext s) {
            var pirates = IncidentHelpers.FactionOf("Pirate");
            IncidentHelpers.SpawnWildAnimal(s, "Muffalo", s.Cell(0, 0));
            var small = IncidentHelpers.Fire(s, "SrPoaching", pirates, 1500f);
            var large = IncidentHelpers.Fire(s, "SrPoaching", pirates, 3000f);
            s.Check(small.Fired && large.Fired, "both Poaching incidents should fire; small=" + small.Fired + " large=" + large.Fired + "; log: " + string.Join(" | ", small.LogLines.Concat(large.LogLines)));
            s.Check(small.Pawns.Count >= 5, "a 1500 point poaching group should have at least 5 pawns, not the single pawn of the lowest points; got " + small.Pawns.Count);
            s.Check(large.Pawns.Count > small.Pawns.Count, "a 3000 point group should be larger than a 1500 point group; got " + large.Pawns.Count + " and " + small.Pawns.Count);
            s.RequireNoLogErrors();
        }

        [Scenario("poaching-and-logging-refuse-quietly-without-their-target", MapSize = 60, MaxSeconds = 60)]
        static void RefuseQuietly(ScenarioContext s) {
            IncidentHelpers.CheckRefusesThenAccepts(s, "SrPoaching", () => IncidentHelpers.SpawnWildAnimal(s, "Muffalo", s.Cell(0, 0)));
            foreach (var animal in s.Map.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Animal).ToList()) animal.Destroy();
            IncidentHelpers.CheckRefusesThenAccepts(s, "SrLogging", () => IncidentHelpers.SpawnTree(s, s.Cell(0, 0)));
        }
    }
}
