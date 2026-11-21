using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimhunterTestRunner.Scenarios
{
    static class Passers
    {
        [Scenario("caravan-hostile-trader-caravan-passes-through-and-tells-the-player", MapSize = 100, MaxSeconds = 150)]
        static void CaravanPassesThrough(ScenarioContext s) {
            var outlanders = IncidentHelpers.FactionOf("OutlanderRough");
            IncidentHelpers.MakeNeutral("OutlanderRough");
            var logBefore = IncidentHelpers.ModLogCount();
            var refused = IncidentHelpers.Fire(s, "SrHositleCaravanPassing", outlanders, -1f);
            s.Check(!refused.Fired && refused.Pawns.Count == 0 && refused.Letters.Count == 0, "a caravan of a faction that is not hostile should not come; fired=" + refused.Fired + " pawns=" + refused.Pawns.Count);

            IncidentHelpers.MakeHostile("OutlanderRough");
            var run = IncidentHelpers.Fire(s, "SrHositleCaravanPassing", outlanders, -1f);
            IncidentHelpers.CheckGroup(s, run, "SrHositleCaravanPassing", outlanders);
            var trader = run.Pawns.FirstOrDefault(p => p.TraderKind != null);
            if (trader == null) { s.Fail("the caravan should hold a trader; " + run.PawnSummary()); return; }
            s.Check(run.Pawns.Count(p => p.RaceProps.packAnimal) >= 1, "the caravan should hold a pack animal carrier; kinds: " + string.Join(", ", run.Pawns.Select(p => p.kindDef.defName)));
            s.Check(run.Pawns.Count(p => p.TraderKind == null && p.RaceProps.Humanlike && p.kindDef.isFighter) >= 1, "the caravan should hold a guard; kinds: " + string.Join(", ", run.Pawns.Select(p => p.kindDef.defName)));

            s.AreEqual(1, run.Letters.Count, "one letter should be shown; letters: " + run.LetterSummary());
            var letter = run.Letters.FirstOrDefault() as ChoiceLetter;
            if (letter == null) { s.Fail("the letter should be a standard letter; letters: " + run.LetterSummary()); return; }
            s.AreEqual(LetterDefOf.NegativeEvent, letter.def, "the letter type");
            var expectedLabel = "SrHostileTraderCaravanPassing".Translate(outlanders.Name, trader.TraderKind.label).CapitalizeFirst().ToString();
            var related = run.Pawns.Any(p => p.relations != null && p.relations.PotentiallyRelatedPawns.Any(o => o.Faction == Faction.OfPlayer));
            s.Check(letter.Label.ToString() == expectedLabel || (related && letter.Label.ToString() == expectedLabel + ": " + "RelationshipAppendedLetterSuffix".Translate().CapitalizeFirst()), "the letter label should be '" + expectedLabel + "' (the game adds the relationship suffix only when a pawn is related to a colonist); got " + letter.Label);
            s.Check(letter.Text.ToString().Contains("LetterTraderCaravanArrival".Translate(outlanders.NameColored, trader.TraderKind.label).CapitalizeFirst().ToString()), "the letter text should hold the caravan text; got " + letter.Text);
            IncidentHelpers.CheckPlace(s, run, letter.lookTargets, "the caravan letter");

            var lords = IncidentHelpers.LordJobs(s.Map);
            s.AreEqual(1, lords.Count, "one lord should run the caravan; lords: " + string.Join(", ", lords));
            s.Check(lords.Any(l => l.StartsWith("LordJobHostileTraderCaravanTravelAndExit/")), "the caravan should run its travel and exit lord job; lords: " + string.Join(", ", lords));
            s.Check(run.Pawns.All(p => p.Faction.HostileTo(Faction.OfPlayer)), "every caravan pawn should be hostile to the player");

            IncidentHelpers.CheckGroupLeaves(s, run, "SrHositleCaravanPassing", 30000);
            s.Check(run.Pawns.All(p => !p.Dead && !p.Downed), "the caravan should leave unharmed");
            IncidentHelpers.CheckModLogQuiet(s, logBefore, "during a caravan run");
            s.RequireNoLogErrors();
            s.RequireNoPlayerLogErrors();
        }

        [Scenario("caravan-hostile-trader-caravan-covers-its-escape-when-a-pawn-is-lost", MapSize = 100, MaxSeconds = 120)]
        static void CaravanFights(ScenarioContext s) {
            var outlanders = IncidentHelpers.FactionOf("OutlanderRough");
            IncidentHelpers.MakeHostile("OutlanderRough");
            var run = IncidentHelpers.Fire(s, "SrHositleCaravanPassing", outlanders, -1f);
            IncidentHelpers.CheckGroup(s, run, "SrHositleCaravanPassing", outlanders);
            s.Tick(60);
            s.Check(s.Map.lordManager.lords.All(l => l.CurLordToil is LordToil_Travel || l.CurLordToil is LordToil_DefendPoint), "before a loss the caravan should travel; toils: " + string.Join(", ", IncidentHelpers.LordJobs(s.Map)));
            var victim = run.Pawns.First(p => p.TraderKind == null && !p.RaceProps.packAnimal);
            victim.Kill(null);
            s.Tick(5);
            s.Check(s.Map.lordManager.lords.Any(l => l.faction == outlanders && l.CurLordToil is LordToil_ExitMapTraderFighting), "after a pawn is lost the caravan should cover its escape; toils: " + string.Join(", ", IncidentHelpers.LordJobs(s.Map)));
            var logBefore = IncidentHelpers.ModLogCount();
            IncidentHelpers.CheckGroupLeaves(s, run, "SrHositleCaravanPassing", 30000);
            IncidentHelpers.CheckModLogQuiet(s, logBefore, "while the caravan covers its escape and leaves");
            s.RequireNoLogErrors();
            s.RequireNoPlayerLogErrors();
        }

        [Scenario("traveler-hostile-traveler-group-passes-through-and-tells-the-player", MapSize = 100, MaxSeconds = 150)]
        static void TravelerPassesThrough(ScenarioContext s) {
            var pirates = IncidentHelpers.FactionOf("Pirate");
            var logBefore = IncidentHelpers.ModLogCount();
            var without = IncidentHelpers.Fire(s, "SrHositleTraveler", pirates, 1500f);
            s.Check(without.Pawns.Count == 0 && without.Messages.Count == 0, "no travelers should come to a map without colonists; pawns=" + without.Pawns.Count);
            s.SpawnColonist(s.Cell(0, 0));
            var run = IncidentHelpers.Fire(s, "SrHositleTraveler", pirates, 1500f);
            IncidentHelpers.CheckGroup(s, run, "SrHositleTraveler", pirates);
            s.Check(run.Pawns.Count >= 2, "1500 points should bring a group; got " + run.Pawns.Count + " fired=" + run.Fired + " log: " + string.Join(" | ", run.LogLines));
            s.Check(run.Letters.All(l => l.def == LetterDefOf.NeutralEvent && l.Label.ToString() == "LetterLabelNoticedRelatedPawns".Translate().ToString()), "the original sends no letter of its own for a traveler group, only the game's relationship letter; letters: " + run.LetterSummary());
            var expected = "GroupTravelersPassing".Translate(pirates.Name).ToString();
            s.AreEqual(1, run.Messages.Count, "one message should be shown; messages: " + run.MessageSummary());
            s.AreEqual(expected, run.Messages.FirstOrDefault()?.text, "the message text");
            s.AreEqual(MessageTypeDefOf.NeutralEvent, run.Messages.FirstOrDefault()?.def, "the message type");
            IncidentHelpers.CheckPlace(s, run, run.Messages.FirstOrDefault()?.lookTargets, "the message");

            var lords = IncidentHelpers.LordJobs(s.Map);
            s.AreEqual(1, lords.Count, "one lord should run the group; lords: " + string.Join(", ", lords));
            s.Check(lords.Any(l => l.StartsWith("LordJob_TravelAndExit/")), "the group should travel and exit; lords: " + string.Join(", ", lords));
            IncidentHelpers.CheckGroupLeaves(s, run, "SrHositleTraveler", 30000);
            s.Check(run.Pawns.All(p => !p.Dead && !p.Downed), "the group should leave unharmed");
            IncidentHelpers.CheckModLogQuiet(s, logBefore, "during a traveler run");
            s.RequireNoLogErrors();
            s.RequireNoPlayerLogErrors();
        }

        [Scenario("caravan-and-traveler-pick-only-visible-hostile-npc-factions-among-30", MapSize = 100, MaxSeconds = 150)]
        static void OnlyVisibleHostileNpcFactions(ScenarioContext s) {
            IncidentHelpers.AddVisibleFactions(30, "OutlanderCivil", "OutlanderRough", "TribeCivil", "TribeRough", "Pirate");
            s.AreEqual(30, Find.FactionManager.AllFactionsVisible.Count(), "the world should hold 30 visible factions; the list leaves out TribeSavage on purpose, because generating a savage tribe trader logs the vanilla error 'No shuffled Adulthood found for trader of TribeSavage' (PawnBioAndNameGenerator) in about 1 of 400 generations, with or without this mod");
            s.SpawnColonist(s.Cell(0, 0));
            var hidden = Find.FactionManager.AllFactions.Where(f => f.Hidden).ToList();
            s.Check(hidden.Any(f => f.HostileTo(Faction.OfPlayer) && f.def.pawnGroupMakers.Any(m => m.kindDef == PawnGroupKindDefOf.Combat)), "the world should hold a hidden hostile faction with a Combat group for the traveler check to be able to fail; hidden: " + string.Join(", ", hidden.Select(f => f.def.defName)));
            foreach (var incident in new[] { "SrHositleCaravanPassing", "SrHositleTraveler" }) {
                var candidates = Find.FactionManager.AllFactions.Where(f => !f.Hidden && !f.IsPlayer && f.HostileTo(Faction.OfPlayer) && f.def.pawnGroupMakers.Any(m => m.kindDef == (incident == "SrHositleTraveler" ? PawnGroupKindDefOf.Combat : PawnGroupKindDefOf.Trader))).ToList();
                s.Check(candidates.Count > 0, "the world should hold a visible hostile faction that can send " + incident);
                foreach (var f in candidates) f.hidden = true;
                var hiddenRun = IncidentHelpers.Fire(s, incident, null, incident == "SrHositleTraveler" ? 1500f : -1f);
                foreach (var f in candidates) f.hidden = null;
                s.Check(hiddenRun.Pawns.Count == 0, incident + " should send no one when every faction that could send it is hidden; got " + hiddenRun.PawnSummary());
                IncidentHelpers.ClearGroup(s, hiddenRun);
            }
            var logBefore = IncidentHelpers.ModLogCount();

            var seen = new System.Collections.Generic.List<string>();
            var unfired = new System.Collections.Generic.List<string>();
            foreach (var incident in new[] { "SrHositleCaravanPassing", "SrHositleTraveler" }) {
                for (var i = 0; i < 10; i++) {
                    var run = IncidentHelpers.Fire(s, incident, null, incident == "SrHositleTraveler" ? 1500f : -1f);
                    if (!run.Fired || run.Pawns.Count == 0) unfired.Add(incident + " " + i + ": " + string.Join(" | ", run.LogLines));
                    foreach (var faction in run.Pawns.Select(p => p.Faction).Distinct()) {
                        seen.Add(incident + ":" + faction.Name);
                        IncidentHelpers.CheckPickedFaction(s, incident, faction);
                    }
                    IncidentHelpers.ClearGroup(s, run);
                }
            }
            s.AreEqual("", string.Join(" || ", unfired), "the firings that did not bring a group");
            s.AreEqual(20, seen.Count, "every firing should have put one faction on the map; seen: " + string.Join(", ", seen));
            IncidentHelpers.CheckModLogQuiet(s, logBefore, "across 20 firings");
            s.RequireNoLogErrors();
        }

        [Scenario("odyssey-traders-guild-does-not-walk-in-as-hostile-travelers", MapSize = 100, MaxSeconds = 150, Mods = "ludeon.rimworld.odyssey")]
        static void GuildDoesNotWalkIn(ScenarioContext s) {
            var guild = IncidentHelpers.FactionOf("TradersGuild");
            s.Check(guild != null, "the Odyssey world should hold the traders guild");
            s.SpawnColonist(s.Cell(0, 0));
            IncidentHelpers.MakeHostile("TradersGuild");
            s.Check(guild.HostileTo(Faction.OfPlayer) && !guild.Hidden, "the guild should be hostile and visible for the check");
            s.Check(!guild.def.pawnGroupMakers.Any(m => m.kindDef == PawnGroupKindDefOf.Trader), "the guild has no Trader group, so it cannot send a caravan");
            s.Check(guild.def.pawnGroupMakers.Any(m => m.kindDef == PawnGroupKindDefOf.Combat), "the guild should have the Combat group the check needs");
            var others = Find.FactionManager.AllFactions.Where(f => f != guild && !f.Hidden && !f.IsPlayer).ToList();
            foreach (var f in others) f.hidden = true;
            var run = IncidentHelpers.Fire(s, "SrHositleTraveler", null, 1500f);
            foreach (var f in others) f.hidden = null;
            s.Check(run.Pawns.All(p => p.Faction != guild), "SrHositleTraveler brought the hostile traders guild on foot onto a surface map; got " + run.PawnSummary());
            IncidentHelpers.ClearGroup(s, run);

            IncidentHelpers.MakeHostile("OutlanderRough");
            var control = IncidentHelpers.Fire(s, "SrHositleTraveler", null, 1500f);
            s.Check(control.Pawns.Count > 0 && control.Pawns.All(p => p.Faction != guild), "SrHositleTraveler should still bring a hostile faction that may walk in; got " + control.PawnSummary());
            IncidentHelpers.ClearGroup(s, control);
            s.RequireNoLogErrors();
        }
    }
}
