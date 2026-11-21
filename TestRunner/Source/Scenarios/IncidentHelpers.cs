using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using Verse;
using HarmonyLib;
using Verse.AI;
using Verse.AI.Group;
using System.Xml.Linq;

namespace RimhunterTestRunner.Scenarios
{
    sealed class IncidentRun
    {
        public bool Fired;
        public List<Letter> Letters = new List<Letter>();
        public List<Message> Messages = new List<Message>();
        public List<Pawn> Pawns = new List<Pawn>();
        public List<string> LogLines = new List<string>();
        public IntVec3 SpawnCenter;
        public Dictionary<string, List<Pawn>> PawnsByFaction = new Dictionary<string, List<Pawn>>();

        public string LetterSummary() => string.Join(" | ", Letters.Select(l => l.Label.ToString() + " / " + (l as ChoiceLetter)?.Text.ToString()));
        public string MessageSummary() => string.Join(" | ", Messages.Select(m => m.text));
        public string PawnSummary() => string.Join("; ", PawnsByFaction.Select(kv => kv.Key + ": " + kv.Value.Count));
    }

    static class IncidentHelpers
    {
        public static Faction FactionOf(string defName) => Find.FactionManager.FirstFactionOfDef(FactionDef.Named(defName));

        public static IncidentRun Fire(ScenarioContext s, string incidentDefName, Faction faction, float points) {
            var def = DefDatabase<IncidentDef>.GetNamed(incidentDefName);
            var lettersBefore = Find.LetterStack.LettersListForReading.Count;
            var messagesBefore = Find.Archive.ArchivablesListForReading.OfType<Message>().Count();
            var pawnsBefore = new HashSet<Pawn>(s.Map.mapPawns.AllPawnsSpawned);
            var logBefore = Log.Messages.Count();
            var parms = new IncidentParms { target = s.Map, points = points, faction = faction };
            var run = new IncidentRun { Fired = def.Worker.TryExecute(parms) };
            run.SpawnCenter = parms.spawnCenter;
            run.LogLines = Log.Messages.Skip(logBefore).Select(m => m.type + " " + m.text).Concat(new[] { "parms " + parms }).ToList();
            Observe(s, run, lettersBefore, messagesBefore, pawnsBefore);
            return run;
        }

        public static void Observe(ScenarioContext s, IncidentRun run, int lettersBefore, int messagesBefore, HashSet<Pawn> pawnsBefore) {
            run.Letters = Find.LetterStack.LettersListForReading.Skip(lettersBefore).ToList();
            run.Messages = Find.Archive.ArchivablesListForReading.OfType<Message>().Skip(messagesBefore).ToList();
            run.Pawns = s.Map.mapPawns.AllPawnsSpawned.Where(p => !pawnsBefore.Contains(p) && p.Faction != null && !p.Faction.IsPlayer).ToList();
            run.PawnsByFaction = run.Pawns.GroupBy(p => p.Faction.Name).ToDictionary(g => g.Key, g => g.ToList());
        }

        public static List<Message> NewMessages(int before) => Find.Archive.ArchivablesListForReading.OfType<Message>().Skip(before).ToList();

        public static int MessageCount() => Find.Archive.ArchivablesListForReading.OfType<Message>().Count();

        public static List<string> LordJobs(Map map) =>
            map.lordManager.lords.Select(l => l.LordJob.GetType().Name + "/" + l.CurLordToil.GetType().Name + "/" + l.faction.Name + "/" + l.ownedPawns.Count).ToList();

        public static string DutyOf(Pawn p) => p.mindState?.duty?.def.defName ?? "none";

        public static Pawn SpawnWildAnimal(ScenarioContext s, string kindDefName, IntVec3 cell) {
            var pawn = PawnGenerator.GeneratePawn(PawnKindDef.Named(kindDefName), null);
            GenSpawn.Spawn(pawn, cell, s.Map);
            return pawn;
        }

        public static Plant SpawnTree(ScenarioContext s, IntVec3 cell) {
            var tree = (Plant) ThingMaker.MakeThing(ThingDef.Named("Plant_TreeOak"));
            tree.Growth = 1f;
            GenSpawn.Spawn(tree, cell, s.Map);
            return tree;
        }

        public static void CheckGroup(ScenarioContext s, IncidentRun run, string incident, Faction faction) {
            s.Check(run.Fired, "the incident " + incident + " should fire; log: " + string.Join(" | ", run.LogLines));
            s.Check(run.Pawns.Count >= 1, "the incident " + incident + " should bring pawns");
            s.Check(run.PawnsByFaction.Keys.SequenceEqual(new[] { faction.Name }), "the pawns of " + incident + " should all be of " + faction.Name + "; got " + run.PawnSummary());
        }

        public static void CheckLetter(ScenarioContext s, IncidentRun run, string strategyDefName, Faction faction) {
            var strategy = DefDatabase<RaidStrategyDef>.GetNamed(strategyDefName);
            s.AreEqual(1, run.Letters.Count, "one letter should be shown for " + strategyDefName + "; letters: " + run.LetterSummary());
            var letter = run.Letters.FirstOrDefault() as ChoiceLetter;
            if (letter == null) { s.Fail("the letter for " + strategyDefName + " should be a standard letter; letters: " + run.LetterSummary()); return; }
            s.AreEqual(LetterDefOf.ThreatSmall, letter.def, "the letter type of " + strategyDefName);
            s.Check(letter.Label.ToString().StartsWith(strategy.letterLabelEnemy) && letter.Label.ToString().Contains(faction.Name), "the letter label should start with '" + strategy.letterLabelEnemy + "' and name " + faction.Name + "; got " + letter.Label);
            s.Check(letter.Text.ToString().Contains(strategy.arrivalTextEnemy), "the letter text should hold '" + strategy.arrivalTextEnemy + "'; got " + letter.Text);
            CheckPlace(s, run, letter.lookTargets, "the letter for " + strategyDefName);
        }

        public static void CheckPlace(ScenarioContext s, IncidentRun run, LookTargets lookTargets, string what) {
            var target = lookTargets?.TryGetPrimaryTarget() ?? RimWorld.Planet.GlobalTargetInfo.Invalid;
            s.Check(target.IsValid && target.Thing is Pawn lookPawn && run.Pawns.Contains(lookPawn) && target.Map == s.Map, what + " should look at one of the group's pawns on this map; it looks at " + target);
            if (target.IsValid && run.SpawnCenter.IsValid) {
                s.Check(target.Cell.DistanceTo(run.SpawnCenter) <= 12f, what + ": the place " + target.Cell + " should be at the group's arrival " + run.SpawnCenter);
                s.Check(target.Cell.CloseToEdge(s.Map, 12), what + ": the place " + target.Cell + " should be at the map edge the group walks in from");
            }
        }

        public static void ClearGroup(ScenarioContext s, IncidentRun run) {
            foreach (var pawn in run.Pawns.ToList()) pawn.Destroy();
            foreach (var lord in s.Map.lordManager.lords.ToList()) s.Map.lordManager.RemoveLord(lord);
        }

        public static void CheckPickedFaction(ScenarioContext s, string incident, Faction faction) {
            s.Check(Find.FactionManager.AllFactions.Contains(faction), incident + " picked " + faction.Name + ", which is not a faction of the world");
            s.Check(!faction.Hidden, incident + " picked the hidden faction " + faction.Name + " (" + faction.def.defName + ")");
            s.Check(!faction.IsPlayer, incident + " picked the player's faction");
            s.Check(faction.HostileTo(Faction.OfPlayer), incident + " picked " + faction.Name + ", which is not hostile to the player");
        }

        public static void MakeHostile(string factionDefName) {
            var faction = FactionOf(factionDefName);
            faction.TryAffectGoodwillWith(Faction.OfPlayer, -200, false, false);
        }

        public static void MakeNeutral(string factionDefName) {
            var faction = FactionOf(factionDefName);
            faction.TryAffectGoodwillWith(Faction.OfPlayer, -faction.GoodwillWith(Faction.OfPlayer), false, false);
        }

        public static void CheckGroupLeaves(ScenarioContext s, IncidentRun run, string incident, int maxTicks) {
            var gone = s.RunUntil(() => run.Pawns.All(p => !p.Spawned), maxTicks);
            s.Check(gone, "the group of " + incident + " should leave the map; still on the map: " + string.Join(", ", run.Pawns.Where(p => p.Spawned).Select(p => p.LabelShort + " at " + p.Position + " duty=" + DutyOf(p))) + "; lords: " + string.Join(", ", LordJobs(s.Map)));
        }

        public static void CheckLeaveMessage(ScenarioContext s, List<Message> messages, string key, Faction faction) {
            var expected = key.Translate(faction.def.pawnsPlural.CapitalizeFirst(), faction.Name).ToString().CapitalizeFirst();
            s.Check(messages.Any(m => m.text == expected), "the player should be told '" + expected + "'; messages: " + string.Join(" | ", messages.Select(m => m.text)));
        }

        public static void CheckModLogQuiet(ScenarioContext s, int logBefore, string when) =>
            s.AreEqual("", string.Join(" | ", ModLogLines(logBefore)), "the mod should log nothing " + when);

        public static void CheckRefusesThenAccepts(ScenarioContext s, string incident, System.Action addWhatItNeeds) {
            var logBefore = ModLogCount();
            var def = DefDatabase<IncidentDef>.GetNamed(incident);
            var parms = new IncidentParms { target = s.Map, points = 800f };
            s.Check(!def.Worker.CanFireNow(parms), incident + " should not be able to fire before the map has what it needs");
            CheckModLogQuiet(s, logBefore, "when " + incident + " cannot fire");
            addWhatItNeeds();
            s.Tick(1);
            s.Check(def.Worker.CanFireNow(parms), incident + " should be able to fire once the map has what it needs, so the refusal came from what was missing");
            CheckModLogQuiet(s, logBefore, "when " + incident + " can fire");
            s.RequireNoLogErrors();
        }

        public static int ModLogCount() => ModLogLines(0).Count;

        public static List<string> ModLogLines(int skip) =>
            Log.Messages.Where(m => m.text.StartsWith("[SR.ModRimWorld.RaidExtension]")).Skip(skip).Select(m => m.type + " " + m.text).ToList();

        public static string Dump(ScenarioContext s, IncidentRun run) =>
            string.Join("; ", run.Pawns.Select(p => p.LabelShort + " spawned=" + p.Spawned + " pos=" + p.Position + " cur=" + p.CurJobDef?.defName + " duty=" + DutyOf(p) + " speed=" + p.GetStatValue(StatDefOf.PlantWorkSpeed) + " off=" + p.WorkTypeIsDisabled(WorkTypeDefOf.Growing) + " skillOff=" + p.skills?.GetSkill(SkillDefOf.Plants).TotallyDisabled + " work=" + (p.jobs?.curDriver as JobDriver_PlantWork != null ? Traverse.Create(p.jobs.curDriver).Field("workDone").GetValue<float>().ToString() : "-") + " jobs=" + string.Join(",", s.JobDefsOf(p).Distinct())));

        public static void AddVisibleFactions(int total, params string[] defNames) {
            if (defNames.Length == 0) defNames = new[] { "OutlanderCivil", "OutlanderRough", "TribeCivil", "TribeRough", "Pirate", "TribeSavage" };
            var defs = defNames
                .Select(n => DefDatabase<FactionDef>.GetNamed(n)).ToList();
            var i = 0;
            while (Find.FactionManager.AllFactionsVisible.Count() < total) {
                var def = defs[i++ % defs.Count];
                Find.FactionManager.Add(FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def)));
            }
        }

        public static int Trees(Map map) => map.listerThings.AllThings.Count(t => t is Plant p && p.def.plant.IsTree && p.Spawned);

        public static IEnumerable<Pawn> Combatants(Map map) =>
            map.mapPawns.AllPawnsSpawned.Where(p => p.Faction != null && !p.Faction.IsPlayer && p.Faction.def.humanlikeFaction);

        public static List<Letter> NewLetters(int before) => Find.LetterStack.LettersListForReading.Skip(before).ToList();

        public static bool Standing(Pawn p) => p.Spawned && !p.Downed && !p.Dead;

        static IEnumerable<string> KeyedKeysOf(ModContentPack mod) {
            var keyed = Path.Combine(mod.RootDir, "Languages", "English", "Keyed");
            if (!Directory.Exists(keyed)) return Enumerable.Empty<string>();
            return Directory.GetFiles(keyed, "*.xml").SelectMany(f => XDocument.Load(f).Root.Elements().Select(e => e.Name.LocalName));
        }

        public static void CheckNoClash(ScenarioContext s, ModContentPack ours, string ourName, int ourMinDefs, int ourMinKeys, string[] ourIncidents,
                                        ModContentPack theirs, string theirName, int theirMinDefs, int theirMinKeys, string[] theirIncidents, string measuredAgainst) {
            var oursDefs = ours.AllDefs.ToList();
            var theirsDefs = theirs.AllDefs.ToList();
            s.Check(oursDefs.Count >= ourMinDefs, ourName + " should hold at least " + ourMinDefs + " defs; it holds " + oursDefs.Count + " (" + measuredAgainst + ")");
            s.Check(theirsDefs.Count >= theirMinDefs, theirName + " should hold at least " + theirMinDefs + " defs; it holds " + theirsDefs.Count + " (" + measuredAgainst + ")");
            string TypedName(Def d) => d.GetType().FullName + "/" + d.defName;
            s.AreEqual("", string.Join(", ", oursDefs.Select(TypedName).Intersect(theirsDefs.Select(TypedName))), "defs of the same type and defName in both mods");
            s.AreEqual("", string.Join(", ", oursDefs.Select(d => d.defName).Intersect(theirsDefs.Select(d => d.defName)).Distinct()), "defNames shared by both mods, even across different def types");
            foreach (var name in ourIncidents)
                s.AreEqual(ours.PackageId, DefDatabase<IncidentDef>.GetNamed(name).modContentPack?.PackageId, "the mod that holds the incident " + name);
            foreach (var name in theirIncidents)
                s.AreEqual(theirs.PackageId, DefDatabase<IncidentDef>.GetNamed(name).modContentPack?.PackageId, "the mod that holds the incident " + name);

            var oursTypes = ours.assemblies.loadedAssemblies.SelectMany(a => a.GetTypes()).Select(t => t.FullName).ToList();
            var theirsTypes = theirs.assemblies.loadedAssemblies.SelectMany(a => a.GetTypes()).Select(t => t.FullName).ToList();
            s.Check(oursTypes.Count >= 20 && theirsTypes.Count >= 20, "both mods should have loaded their code; types: " + oursTypes.Count + " and " + theirsTypes.Count);
            s.AreEqual("", string.Join(", ", oursTypes.Intersect(theirsTypes)), "types with the same full name in both mods' code");

            var language = LanguageDatabase.activeLanguage;
            foreach (var pair in new[] { (ours, ourName, ourMinKeys), (theirs, theirName, theirMinKeys) }) {
                var keys = KeyedKeysOf(pair.Item1).ToList();
                s.Check(keys.Count >= pair.Item3, pair.Item2 + " should ship at least " + pair.Item3 + " English keyed keys; found " + keys.Count + " (" + measuredAgainst + ")");
                var wrong = keys.Where(k => !language.keyedReplacements.TryGetValue(k, out var r) || !Path.GetFullPath(r.fileSourceFullPath).StartsWith(Path.GetFullPath(pair.Item1.RootDir))).ToList();
                s.AreEqual("", string.Join(", ", wrong), "keys of " + pair.Item2 + " that the game took from another mod's file");
            }
            s.AreEqual("", string.Join(", ", KeyedKeysOf(ours).Intersect(KeyedKeysOf(theirs))), "keyed keys in both mods' English files");
        }

        public static Faction EnsureFaction(string defName) {
            var existing = FactionOf(defName);
            if (existing != null) return existing;
            var faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(FactionDef.Named(defName)));
            Find.FactionManager.Add(faction);
            return faction;
        }

        public static void SpawnTreesWhereFree(ScenarioContext s, int count, int dz) {
            for (var i = 0; i < count; i++) {
                var cell = s.Cell(-6 + 3 * (i % 4), dz + 3 * (i / 4));
                if (cell.GetPlant(s.Map) == null) SpawnTree(s, cell);
            }
        }

        public static IncidentRun FireIncident(ScenarioContext s, string incident, Faction faction) {
            if (incident == "SrHositleTraveler" && s.Map.mapPawns.FreeColonistsSpawnedCount == 0) s.SpawnColonist(s.Cell(10, 10));
            var run = Fire(s, incident, faction, incident == "SrHositleCaravanPassing" ? -1f : 1500f);
            CheckGroup(s, run, incident, faction);
            return run;
        }

        public static IncidentRun RunPoaching(ScenarioContext s, string label, Faction faction) {
            foreach (var other in s.Map.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Animal && p.RaceProps.baseHealthScale >= 1.2f).ToList()) other.Destroy(DestroyMode.Vanish);
            var animal = SpawnWildAnimal(s, "Muffalo", s.Cell(8, 8));
            var logBefore = ModLogCount();
            var messagesBefore = MessageCount();
            var run = FireIncident(s, "SrPoaching", faction);
            CheckLetter(s, run, "SrPoaching", faction);
            s.Check(s.RunUntil(() => animal.Dead, 9000), label + ": the poachers should kill the large animal; animal dead=" + animal.Dead + " spawned=" + animal.Spawned + " at " + animal.Position + "; " + string.Join("; ", run.Pawns.Select(p => p.LabelShort + " spawned=" + p.Spawned + " dead=" + p.Dead + " down=" + p.Downed + " pos=" + p.Position + " cur=" + p.CurJobDef?.defName + " duty=" + DutyOf(p) + " weapon=" + p.equipment?.Primary?.def.defName)) + "; lords: " + string.Join(", ", LordJobs(s.Map)));
            CheckGroupLeaves(s, run, label + " SrPoaching", 15000);
            CheckLeaveMessage(s, NewMessages(messagesBefore), "SrTakePreyExit", faction);
            CheckModLogQuiet(s, logBefore, "during a poaching run, " + label);
            return run;
        }

        public static IncidentRun RunLogging(ScenarioContext s, string label, Faction faction) {
            SpawnTreesWhereFree(s, 12, -10);
            var treesBefore = Trees(s.Map);
            var logBefore = ModLogCount();
            var messagesBefore = MessageCount();
            var run = FireIncident(s, "SrLogging", faction);
            CheckLetter(s, run, "SrLogging", faction);
            foreach (var pawn in run.Pawns.Where(p => !p.skills.GetSkill(SkillDefOf.Plants).TotallyDisabled)) pawn.skills.GetSkill(SkillDefOf.Plants).Level = 12;
            s.Check(s.RunUntil(() => Trees(s.Map) < treesBefore, 15000), label + ": the crew should fell a tree; " + Dump(s, run));
            CheckGroupLeaves(s, run, label + " SrLogging", 30000);
            CheckLeaveMessage(s, NewMessages(messagesBefore), "SrTakeWoodExit", faction);
            CheckModLogQuiet(s, logBefore, "during a logging run, " + label);
            return run;
        }

        public static IncidentRun RunCaravan(ScenarioContext s, string label, Faction faction) {
            var logBefore = ModLogCount();
            var run = FireIncident(s, "SrHositleCaravanPassing", faction);
            s.Check(run.Pawns.Any(p => p.TraderKind != null), label + ": the caravan should hold a trader; " + run.PawnSummary());
            s.AreEqual(1, run.Letters.Count, label + ": one caravan letter should be shown; letters: " + run.LetterSummary());
            s.AreEqual(LetterDefOf.NegativeEvent, run.Letters.FirstOrDefault()?.def, label + ": the caravan letter type");
            CheckPlace(s, run, run.Letters.FirstOrDefault()?.lookTargets, label + " caravan letter");
            CheckGroupLeaves(s, run, label + " SrHositleCaravanPassing", 30000);
            CheckModLogQuiet(s, logBefore, "during a caravan run, " + label);
            return run;
        }

        public static IncidentRun RunTraveler(ScenarioContext s, string label, Faction faction) {
            var logBefore = ModLogCount();
            var witness = s.SpawnColonist(s.Cell(10, 10));
            var run = FireIncident(s, "SrHositleTraveler", faction);
            s.AreEqual(1, run.Messages.Count, label + ": one traveler message should be shown; messages: " + run.MessageSummary());
            s.AreEqual("GroupTravelersPassing".Translate(faction.Name).ToString(), run.Messages.FirstOrDefault()?.text, label + ": the traveler message text");
            CheckPlace(s, run, run.Messages.FirstOrDefault()?.lookTargets, label + " traveler message");
            CheckGroupLeaves(s, run, label + " SrHositleTraveler", 30000);
            CheckModLogQuiet(s, logBefore, "during a traveler run, " + label);
            witness.Destroy(DestroyMode.Vanish);
            return run;
        }

        public static List<Pawn> WithWorkSettings(ScenarioContext s, IEnumerable<Pawn> pawns, string what) {
            var all = pawns.ToList();
            var without = all.Where(p => p.workSettings == null).ToList();
            s.AreEqual("", string.Join(", ", without.Where(p => p.RaceProps.Humanlike).Select(p => p.LabelShort)), what + ": the humanlike pawns without a work settings object");
            return all.Where(p => p.workSettings != null).ToList();
        }
    }
}
