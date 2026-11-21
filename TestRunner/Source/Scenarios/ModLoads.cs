using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse.AI;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class ModLoads
    {
        const string Namespace = "SR.ModRimWorld.RaidExtension.";

        [Scenario("startup-loads-mod-content-with-no-errors", MapSize = 40)]
        static void LoadsContent(ScenarioContext s) {
            s.Check(Current.ProgramState == ProgramState.Playing, "the game should reach a map");
            s.Check(Find.CurrentMap == s.Map, "the scenario map should be the current map");

            var running = LoadedModManager.RunningModsListForReading.Select(m => m.PackageId).ToList();
            s.Check(running.Contains("rimhunter.raidextension"), "the mod rimhunter.raidextension should be running; the running mods are: " + string.Join(", ", running));

            var incidents = new Dictionary<string, string[]> {
                ["SrPoaching"] = new[] { "IncidentWorkerPoaching", "ThreatSmall" },
                ["SrLogging"] = new[] { "IncidentWorkerLogging", "ThreatSmall" },
                ["SrHositleCaravanPassing"] = new[] { "IncidentWorkerHostileTraderCaravanPassing", "Misc" },
                ["SrHositleTraveler"] = new[] { "IncidentWorkerHostileTraveler", "Misc" },
            };
            foreach (var pair in incidents) {
                var def = DefDatabase<IncidentDef>.GetNamedSilentFail(pair.Key);
                s.Check(def != null, "the incident " + pair.Key + " should be loaded");
                s.AreEqual(Namespace + pair.Value[0], def?.workerClass?.FullName, "the worker class of incident " + pair.Key);
                s.AreEqual(pair.Value[1], def?.category?.defName, "the category of incident " + pair.Key);
            }

            var strategies = new Dictionary<string, string> {
                ["SrPoaching"] = "RaidStrategyWorkerPoaching",
                ["SrLogging"] = "RaidStrategyWorkerLogging",
            };
            foreach (var pair in strategies) {
                var def = DefDatabase<RaidStrategyDef>.GetNamedSilentFail(pair.Key);
                s.Check(def != null, "the raid strategy " + pair.Key + " should be loaded");
                s.AreEqual(Namespace + pair.Value, def?.workerClass?.FullName, "the worker class of raid strategy " + pair.Key);
            }

            var job = DefDatabase<JobDef>.GetNamedSilentFail("SrTakeWoodExit");
            s.Check(job != null, "the job SrTakeWoodExit should be loaded");
            s.AreEqual(Namespace + "JobDriverTakeWoodExit", job?.driverClass?.FullName, "the driver class of job SrTakeWoodExit");

            var duties = new[] { "SrLogging", "SrTakeWoodExit", "SrPoaching", "SrTakePreyExit" };
            s.AreEqual("", string.Join(",", duties.Where(n => DefDatabase<DutyDef>.GetNamedSilentFail(n) == null)), "the duties that did not load");

            s.RequireNoLogErrors();
            s.RequireNoPlayerLogErrors();
        }
    }
}
