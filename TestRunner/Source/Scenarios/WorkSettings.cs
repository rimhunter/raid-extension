using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimhunterTestRunner.Scenarios
{
    static class WorkSettings
    {
        static readonly List<string> Asked = new List<string>();

        static void RecordWillingAsked(Thing plant, Pawn pawn) =>
            Asked.Add(pawn.LabelShort + " player=" + (pawn.Faction?.IsPlayer == true) + " initialized=" + pawn.workSettings?.Initialized);

        static void AskWorkIsActive(Thing plant, Pawn pawn) => pawn.workSettings.WorkIsActive(WorkTypeDefOf.Growing);

        static List<Pawn> FreshRaiders(Faction faction, int count) =>
            Enumerable.Range(0, count).Select(i => PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                PawnKindDefOf.Pirate, faction, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true))).ToList();

        static string States(IEnumerable<Pawn> pawns) =>
            string.Join(", ", pawns.Where(p => p.workSettings != null).Select(p => p.LabelShort + "=" + p.workSettings.Initialized));

        static readonly string[] Incidents = { "SrPoaching", "SrLogging", "SrHositleCaravanPassing", "SrHositleTraveler" };

        static void Prepare(ScenarioContext s) {
            IncidentHelpers.SpawnWildAnimal(s, "Muffalo", s.Cell(6, 6));
            IncidentHelpers.SpawnTreesWhereFree(s, 12, -4);
            IncidentHelpers.MakeHostile("OutlanderRough");
        }

        static IncidentRun FireIncident(ScenarioContext s, string incident) =>
            IncidentHelpers.FireIncident(s, incident, IncidentHelpers.FactionOf(incident == "SrHositleCaravanPassing" ? "OutlanderRough" : "Pirate"));

        [Scenario("work-settings-incident-pawns-have-initialized-work-settings", MapSize = 100, MaxSeconds = 150)]
        static void IncidentPawnsAreInitialized(ScenarioContext s) {
            var colonist = s.SpawnColonist(s.Cell(0, 0));
            s.Check(colonist.workSettings.Initialized, "a colonist should have initialized work settings");

            var fresh = FreshRaiders(IncidentHelpers.FactionOf("Pirate"), 4);
            var freshHumans = IncidentHelpers.WithWorkSettings(s, fresh, "the freshly generated raiders");
            s.AreEqual(4, freshHumans.Count, "the freshly generated raiders with a work settings object");
            s.AreEqual("", string.Join(", ", freshHumans.Where(p => p.workSettings.Initialized).Select(p => p.LabelShort)), "the freshly generated raiders whose work settings are initialized (the game sets priorities only for player pawns, and forceGenerateNewPawn skips the world pawns it would redress, which the mod's own earlier pawns can be): this is the contrast that lets the check below fail");
            foreach (var pawn in fresh) pawn.Destroy(DestroyMode.Vanish);

            Prepare(s);
            var seen = 0;
            foreach (var incident in Incidents) {
                var run = FireIncident(s, incident);
                var humans = IncidentHelpers.WithWorkSettings(s, run.Pawns, incident);
                s.Check(humans.Count >= 1, incident + " should bring at least one pawn with a work settings object; got " + run.PawnSummary());
                seen += humans.Count;
                s.AreEqual("", string.Join(", ", humans.Where(p => !p.workSettings.Initialized).Select(p => p.LabelShort)), incident + ": the pawns whose work settings are not initialized; all: " + States(humans));
                IncidentHelpers.ClearGroup(s, run);
            }
            s.Check(seen >= 4, "the four incidents should have brought at least 4 pawns with work settings in all; got " + seen);
            s.RequireNoLogErrors();
        }

        [Scenario("work-settings-logging-asks-the-tree-lover-check-of-initialized-raiders", MapSize = 100, MaxSeconds = 150)]
        static void LoggingAsksAboutInitializedRaiders(ScenarioContext s) {
            var pirates = IncidentHelpers.FactionOf("Pirate");
            for (var i = 0; i < 12; i++) IncidentHelpers.SpawnTree(s, s.Cell(-6 + 3 * (i % 4), -4 + 3 * (i / 4)));
            Asked.Clear();
            var harmony = new Harmony("rimhunter.raidextension.testrunner.work-settings-probe");
            harmony.Patch(AccessTools.Method(typeof(PlantUtility), nameof(PlantUtility.PawnWillingToCutPlant_Job)), prefix: new HarmonyMethod(typeof(WorkSettings), nameof(RecordWillingAsked)));
            try {
                var run = IncidentHelpers.Fire(s, "SrLogging", pirates, 1500f);
                IncidentHelpers.CheckGroup(s, run, "SrLogging", pirates);
                var asked = s.RunUntil(() => Asked.Count > 0, 3000);
                s.Check(asked, "the loggers should ask the game whether a raider may cut a tree");
                s.AreEqual("", string.Join(" | ", Asked.Where(a => !a.Contains("player=False initialized=True"))), "the askers that are not raiders with initialized work settings");
                IncidentHelpers.ClearGroup(s, run);
            } finally {
                harmony.UnpatchAll(harmony.Id);
            }
            s.RequireNoLogErrors();
        }

        [Scenario("work-settings-a-patch-asking-work-is-active-of-incident-pawns-logs-nothing", MapSize = 100, MaxSeconds = 150)]
        static void PatchAskingWorkIsActiveLogsNothing(ScenarioContext s) {
            s.SpawnColonist(s.Cell(10, 10));
            Prepare(s);
            var harmony = new Harmony("rimhunter.raidextension.testrunner.work-settings-asker");
            harmony.Patch(AccessTools.Method(typeof(PlantUtility), nameof(PlantUtility.PawnWillingToCutPlant_Job)), postfix: new HarmonyMethod(typeof(WorkSettings), nameof(AskWorkIsActive)));
            try {
                var asked = 0;
                foreach (var incident in Incidents) {
                    var run = FireIncident(s, incident);
                    foreach (var pawn in IncidentHelpers.WithWorkSettings(s, run.Pawns, incident)) {
                        pawn.workSettings.WorkIsActive(WorkTypeDefOf.Growing);
                        asked++;
                    }
                    if (incident == "SrLogging") s.Tick(600);
                    IncidentHelpers.ClearGroup(s, run);
                }
                s.Check(asked >= 4, "WorkIsActive should have been asked of at least 4 incident pawns; got " + asked);
            } finally {
                harmony.UnpatchAll(harmony.Id);
            }
            s.RequireNoLogErrors();
        }

        static readonly string[] CommonJobs = { "Goto", "GotoWander", "Wait", "Wait_Wander", "Wait_Combat", "Wait_MaintainPosture", "AttackMelee", "AttackStatic" };

        static string[] AllowedJobs(string incident) {
            switch (incident) {
                case "SrPoaching": return CommonJobs.Concat(new[] { "Steal" }).ToArray();
                case "SrLogging": return CommonJobs.Concat(new[] { "CutPlant", "SrTakeWoodExit" }).ToArray();
                default: return CommonJobs;
            }
        }

        static HashSet<string> SampleJobs(ScenarioContext s, IEnumerable<Pawn> pawns, int rounds) {
            var seen = new HashSet<string>();
            var list = pawns.ToList();
            for (var i = 0; i < rounds; i++) {
                s.Tick(60);
                foreach (var pawn in list.Where(p => p.Spawned && p.CurJobDef != null)) seen.Add(pawn.CurJobDef.defName);
            }
            return seen;
        }

        [Scenario("work-settings-initialized-incident-pawns-take-only-the-jobs-their-duties-give", MapSize = 100, MaxSeconds = 150)]
        static void InitializedPawnsTakeOnlyDutyJobs(ScenarioContext s) {
            s.SpawnColonist(s.Cell(20, 20));
            var zone = s.Stockpile(new CellRect(s.Cell(-3, 5).x, s.Cell(-3, 5).z, 5, 5), "Steel");
            for (var i = 0; i < 6; i++) s.SpawnItem("Steel", 75, s.Cell(-12 + i, 0));

            var helper = s.SpawnColonist(s.Cell(-6, 2));
            var colonyJobs = new[] { "HaulToCell", "HaulToContainer" };
            s.Check(s.RunUntil(() => helper.CurJobDef != null && colonyJobs.Contains(helper.CurJobDef.defName), 1500), "control: a colonist next to the stockpile and the loose steel should take a hauling job, so a colony job is available in this setup; its job is " + helper.CurJobDef?.defName);
            helper.Destroy(DestroyMode.Vanish);

            var pirates = IncidentHelpers.FactionOf("Pirate");
            IncidentHelpers.MakeHostile("Pirate");
            var guards = PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms { groupKind = PawnGroupKindDefOf.Combat, tile = s.Map.Tile, faction = pirates, points = 600f }).Where(p => p.RaceProps.Humanlike).ToList();
            foreach (var guard in guards) {
                GenSpawn.Spawn(guard, s.Cell(-6, 2), s.Map);
                guard.workSettings.EnableAndInitializeIfNotAlreadyInitialized();
            }
            LordMaker.MakeNewLord(pirates, new LordJob_DefendPoint(s.Cell(-6, 2)), s.Map, guards);
            var guardJobs = SampleJobs(s, guards, 20);
            s.AreEqual("", string.Join(", ", guardJobs.Where(j => !CommonJobs.Contains(j))), "the jobs the initialized hostile pawns with the Defend duty took next to the stockpile and the steel (all: " + string.Join(", ", guardJobs) + "); a colony job is not offered to them");

            var forced = guards.First(g => g.Spawned);
            var steel = s.Map.listerThings.ThingsOfDef(ThingDefOf.Steel).First();
            forced.jobs.StartJob(JobMaker.MakeJob(JobDefOf.HaulToCell, steel, zone.Cells.First()), JobCondition.InterruptForced);
            s.Tick(2);
            var forcedJob = forced.CurJobDef?.defName;
            s.Check(forcedJob != null && !AllowedJobs("SrHositleTraveler").Contains(forcedJob), "control: a pawn forced onto a hauling job should show a job outside the allowed list, so the check below can fail; its job is " + forcedJob);
            foreach (var guard in guards) guard.Destroy(DestroyMode.Vanish);
            foreach (var lord in s.Map.lordManager.lords.ToList()) s.Map.lordManager.RemoveLord(lord);

            Prepare(s);
            foreach (var incident in Incidents) {
                var run = FireIncident(s, incident);
                var used = SampleJobs(s, run.Pawns, 25);
                s.Check(used.Count > 0, incident + ": the incident pawns should have taken some job, for the check to be able to fail");
                s.AreEqual("", string.Join(", ", used.Where(j => !AllowedJobs(incident).Contains(j))), incident + ": the jobs outside the allowed list that the incident pawns took; all: " + string.Join(", ", used));
                IncidentHelpers.ClearGroup(s, run);
            }
            s.RequireNoLogErrors();
        }
    }
}
