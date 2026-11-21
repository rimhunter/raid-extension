using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class FactionalWarTogether
    {
        const string FactionalWarFolder = "external/factional-war";
        const string OurPackageId = "rimhunter.raidextension";
        const string FactionalWarPackageId = "rimhunter.factionalwar";

        static ModContentPack Running(ScenarioContext s, string packageId) {
            var mod = LoadedModManager.RunningModsListForReading.FirstOrDefault(m => m.PackageId == packageId);
            if (mod == null) s.Fail(packageId + " should be running; the running mods are: " + string.Join(", ", LoadedModManager.RunningModsListForReading.Select(m => m.PackageId)));
            return mod;
        }

        static string BuiltFrom(ModContentPack mod) {
            var file = Path.Combine(mod.RootDir, ".built-from");
            return File.Exists(file) ? "Factional War main " + File.ReadAllText(file).Trim() : "Factional War build of unknown commit";
        }

        static bool AtWar(Faction a, Faction b) =>
            a.RelationKindWith(b) == FactionRelationKind.Hostile && b.RelationKindWith(a) == FactionRelationKind.Hostile;

        static string Names(IEnumerable<Faction> factions) => string.Join(", ", factions.Select(f => f.Name));

        [Scenario("together-with-factional-war-both-load-and-an-incident-of-each-runs-with-no-errors", MapSize = 120, MaxSeconds = 280, Mods = FactionalWarFolder)]
        static void BothRun(ScenarioContext s) {
            var ours = Running(s, OurPackageId);
            var theirs = Running(s, FactionalWarPackageId);
            if (ours == null || theirs == null) return;
            var measured = BuiltFrom(theirs);
            var meta = ModLister.GetActiveModWithIdentifier(FactionalWarPackageId, true);
            s.Check(Version.TryParse(meta?.ModVersion, out var version) && version >= new Version(1, 1, 0), "Factional War should be version 1.1.0 or later; the mod list says '" + meta?.ModVersion + "' (" + measured + ")");
            s.AreEqual("Factional War (rimhunter)", meta?.Name, "the name of Factional War in the mod list");
            var order = LoadedModManager.RunningModsListForReading.Select(m => m.PackageId).ToList();
            s.Check(order.Contains("ludeon.rimworld") && order.IndexOf("ludeon.rimworld") < order.IndexOf(OurPackageId) && order.IndexOf("ludeon.rimworld") < order.IndexOf(FactionalWarPackageId), "both mods should load after the game; order: " + string.Join(", ", order));
            IncidentHelpers.CheckNoClash(s,
                ours, "Raid Extension", 11, 3, new[] { "SrPoaching", "SrLogging", "SrHositleCaravanPassing", "SrHositleTraveler" },
                theirs, "Factional War", 23, 19, new[] { "SrFactionWar", "SrFactionWarShellingSiteGenerate", "SrFactionWarContentionSiteGenerate", "SrFactionWarTempCampSiteGenerate" },
                "measured against " + measured);
            s.RequireNoLogErrors();
            s.RequireNoPlayerLogErrors();

            var pirates = IncidentHelpers.FactionOf("Pirate");
            IncidentHelpers.SpawnWildAnimal(s, "Muffalo", s.Cell(0, 0));
            var modLogBefore = IncidentHelpers.ModLogCount();
            var messagesBefore = IncidentHelpers.MessageCount();
            var poaching = IncidentHelpers.Fire(s, "SrPoaching", pirates, 1500f);
            IncidentHelpers.CheckGroup(s, poaching, "SrPoaching", pirates);
            IncidentHelpers.CheckLetter(s, poaching, "SrPoaching", pirates);
            var left = s.RunUntil(() => poaching.Pawns.All(p => !p.Spawned), 20000);
            s.Check(left, "the poachers should leave the map; still on the map: " + string.Join(", ", poaching.Pawns.Where(p => p.Spawned).Select(p => p.LabelShort + " duty=" + IncidentHelpers.DutyOf(p))));
            IncidentHelpers.CheckLeaveMessage(s, IncidentHelpers.NewMessages(messagesBefore), "SrTakePreyExit", pirates);
            IncidentHelpers.CheckModLogQuiet(s, modLogBefore, "during a poaching run beside Factional War");
            s.RequireNoLogErrors();

            IncidentHelpers.ClearGroup(s, poaching);
            var lettersBefore = Find.LetterStack.LettersListForReading.Count;
            messagesBefore = IncidentHelpers.MessageCount();
            var fired = DefDatabase<IncidentDef>.GetNamed("SrFactionWar").Worker.TryExecute(new IncidentParms { target = s.Map, points = 1500f });
            s.Check(fired, "the Factional War incident should fire beside Raid Extension");
            var pawns = IncidentHelpers.Combatants(s.Map).ToList();
            var factions = pawns.Select(p => p.Faction).Distinct().ToList();
            s.AreEqual(2, factions.Count, "two NPC factions should fight, got: " + Names(factions) + " (" + measured + ")");
            if (factions.Count != 2) return;
            s.Check(AtWar(factions[0], factions[1]), "the two factions should be at war by the game's own relations; " + factions[0].Name + "/" + factions[1].Name);
            s.AreEqual(0, pawns.Count(p => p.Dead || p.Downed), "no combatant should be dead or downed before the battle starts");

            var letters = IncidentHelpers.NewLetters(lettersBefore);
            var strategy = DefDatabase<RaidStrategyDef>.GetNamed("SrFactionFirst");
            var letter = letters.FirstOrDefault(l => factions.All(f => l.Label.ToString().Contains(f.Name)));
            s.AreEqual(1, letters.Count, "one letter should be shown for the war; letters: " + string.Join(" | ", letters.Select(l => l.Label.ToString())));
            s.Check(letter != null && letter.Label.ToString().StartsWith(strategy.letterLabelEnemy), "the letter should start with '" + strategy.letterLabelEnemy + "' and name both factions; letters: " + string.Join(" | ", letters.Select(l => l.Label.ToString())));
            s.AreEqual(LetterDefOf.ThreatBig, letter?.def, "the letter type of the war");
            var target = letter?.lookTargets?.TryGetPrimaryTarget() ?? RimWorld.Planet.GlobalTargetInfo.Invalid;
            s.Check(target.IsValid && target.Thing is Pawn lookPawn && pawns.Contains(lookPawn) && target.Map == s.Map, "the letter should look at one of the battle's pawns on this map; it looks at " + target);

            bool Lost(Faction f) => !pawns.Where(p => p.Faction == f).Any(IncidentHelpers.Standing) && pawns.Any(p => p.Faction == f && (p.Dead || p.Downed));
            var over = s.RunUntil(() => factions.Any(Lost), 30000);
            s.Check(over, "one of the two factions should have no standing pawn on the map and a pawn dead or downed at the end of the battle; standing: " + string.Join("; ", factions.Select(f => f.Name + " " + pawns.Count(p => p.Faction == f && IncidentHelpers.Standing(p)) + " of " + pawns.Count(p => p.Faction == f))));

            string Ended(Faction winner, Faction loser) => "SrAssaultFactionVictory".Translate((NamedArgument) winner.def.pawnsPlural.CapitalizeFirst(), (NamedArgument) winner.Name, (NamedArgument) loser.def.pawnsPlural.CapitalizeFirst(), (NamedArgument) loser.Name).ToString();
            string Cleared(Faction winner) => "SrClearBattlefiled".Translate((NamedArgument) winner.def.pawnsPlural.CapitalizeFirst(), (NamedArgument) winner.Name).ToString();
            var expected = new[] { Ended(factions[0], factions[1]), Ended(factions[1], factions[0]), Cleared(factions[0]), Cleared(factions[1]) };
            var told = s.RunUntil(() => IncidentHelpers.NewMessages(messagesBefore).Any(m => expected.Contains(m.text.ToString())), 6000);
            s.Check(told, "the player should be told how the battle ended with one of: " + string.Join(" | ", expected) + "; messages: " + string.Join(" | ", IncidentHelpers.NewMessages(messagesBefore).Select(m => m.text.ToString())));
            s.RequireNoLogErrors();
            s.RequireNoPlayerLogErrors();
        }
    }
}
