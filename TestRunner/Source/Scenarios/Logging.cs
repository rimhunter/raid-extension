using System.Linq;
using RimWorld;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class Logging
    {
        [Scenario("logging-hostile-crew-fells-trees-and-leaves", MapSize = 100, MaxSeconds = 150)]
        static void LoggersFellTreesAndLeave(ScenarioContext s) {
            var pirates = IncidentHelpers.FactionOf("Pirate");
            for (var i = 0; i < 12; i++) IncidentHelpers.SpawnTree(s, s.Cell(-6 + 3 * (i % 4), -4 + 3 * (i / 4)));
            var treesBefore = IncidentHelpers.Trees(s.Map);
            s.AreEqual(12, treesBefore, "the scenario map should hold the 12 trees spawned");

            var logBefore = IncidentHelpers.ModLogCount();
            var run = IncidentHelpers.Fire(s, "SrLogging", pirates, 1500f);
            IncidentHelpers.CheckGroup(s, run, "SrLogging", pirates);
            IncidentHelpers.CheckLetter(s, run, "SrLogging", pirates);

            foreach (var pawn in run.Pawns.Where(p => !p.skills.GetSkill(SkillDefOf.Plants).TotallyDisabled)) pawn.skills.GetSkill(SkillDefOf.Plants).Level = 12;

            var lords = IncidentHelpers.LordJobs(s.Map);
            s.AreEqual(1, lords.Count, "one lord should run the crew; lords: " + string.Join(", ", lords));
            s.Check(lords.Any(l => l.StartsWith("LordJobLogging/")), "the crew should run the logging lord job; lords: " + string.Join(", ", lords));

            var messagesBefore = IncidentHelpers.MessageCount();
            var cutting = s.RunUntil(() => IncidentHelpers.Trees(s.Map) < treesBefore, 15000);
            s.Check(cutting, "the crew should fell a tree; trees left: " + IncidentHelpers.Trees(s.Map) + "; " + IncidentHelpers.Dump(s, run));
            var gone = s.RunUntil(() => run.Pawns.All(p => !p.Spawned), 30000);
            s.Check(gone, "the crew should leave the map; still on the map: " + string.Join(", ", run.Pawns.Where(p => p.Spawned).Select(p => p.LabelShort + " duty=" + IncidentHelpers.DutyOf(p))));
            s.Check(IncidentHelpers.Trees(s.Map) < treesBefore, "trees should have been felled by the time the crew left");
            var messages = IncidentHelpers.NewMessages(messagesBefore);
            IncidentHelpers.CheckLeaveMessage(s, messages, "SrTakeWoodExit", pirates);
            IncidentHelpers.CheckModLogQuiet(s, logBefore, "during a logging run");
            s.RequireNoLogErrors();
            s.RequireNoPlayerLogErrors();
        }
    }
}
