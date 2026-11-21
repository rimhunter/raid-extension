using System.Linq;
using RimWorld;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class PoachingTarget
    {
        [Scenario("poaching-group-leaves-when-the-target-animal-leaves-the-map", MapSize = 100, MaxSeconds = 150)]
        static void GroupLeavesWhenTargetIsGone(ScenarioContext s) {
            var pirates = IncidentHelpers.FactionOf("Pirate");
            foreach (var other in s.Map.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Animal && p.RaceProps.baseHealthScale >= 1.2f).ToList()) other.Destroy(DestroyMode.Vanish);
            var animal = IncidentHelpers.SpawnWildAnimal(s, "Muffalo", s.Cell(8, 8));
            var logBefore = IncidentHelpers.ModLogCount();
            var run = IncidentHelpers.Fire(s, "SrPoaching", pirates, 1500f);
            IncidentHelpers.CheckGroup(s, run, "SrPoaching", pirates);
            s.Check(s.RunUntil(() => run.Pawns.Any(p => IncidentHelpers.DutyOf(p) == "SrPoaching"), 3000), "the poachers should start the Poaching duty");
            animal.DeSpawn();
            s.Check(!animal.Spawned && !animal.Dead, "the animal should have left the map alive, as a fleeing animal does");
            IncidentHelpers.CheckGroupLeaves(s, run, "SrPoaching with the target gone", 15000);
            IncidentHelpers.CheckModLogQuiet(s, logBefore, "when the target animal has left the map");
            s.RequireNoLogErrors();
        }
    }
}
