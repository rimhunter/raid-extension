using System.Linq;
using RimWorld;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class DlcIncidents
    {
        [Scenario("dlc-royalty-all-four-incidents-run", MapSize = 100, MaxSeconds = 90, Mods = "ludeon.rimworld.royalty")]
        static void Royalty(ScenarioContext s) => RunAll(s, "Royalty");

        [Scenario("dlc-anomaly-all-four-incidents-run", MapSize = 100, MaxSeconds = 90, Mods = "ludeon.rimworld.anomaly")]
        static void Anomaly(ScenarioContext s) => RunAll(s, "Anomaly");

        [Scenario("dlc-biotech-all-four-incidents-run", MapSize = 100, MaxSeconds = 90, Mods = "ludeon.rimworld.biotech")]
        static void Biotech(ScenarioContext s) => RunAll(s, "Biotech");

        [Scenario("dlc-ideology-all-four-incidents-run", MapSize = 100, MaxSeconds = 90, Mods = "ludeon.rimworld.ideology")]
        static void Ideology(ScenarioContext s) => RunAll(s, "Ideology");

        [Scenario("dlc-odyssey-all-four-incidents-run", MapSize = 100, MaxSeconds = 90, Mods = "ludeon.rimworld.odyssey")]
        static void Odyssey(ScenarioContext s) => RunAll(s, "Odyssey");

        [Scenario("dlc-all-five-all-four-incidents-run", MapSize = 100, MaxSeconds = 150, Mods = "ludeon.rimworld.royalty ludeon.rimworld.anomaly ludeon.rimworld.biotech ludeon.rimworld.ideology ludeon.rimworld.odyssey")]
        static void AllFive(ScenarioContext s) => RunAll(s, "all five");

        static readonly (string Dlc, string Faction, string Xenotype)[] DlcFactions = {
            ("Royalty", "Empire", null),
            ("Biotech", "TribeRoughNeanderthal", "Neanderthal"),
            ("Biotech", "PirateYttakin", "Yttakin"),
            ("Biotech", "TribeSavageImpid", "Impid"),
            ("Biotech", "OutlanderRoughPig", "Pigskin"),
            ("Biotech", "PirateWaster", null),
            ("Ideology", "TribeCannibal", null),
            ("Ideology", "NudistTribe", null),
            ("Ideology", "CannibalPirate", null),
        };

        [Scenario("odyssey-traders-guild-is-not-picked-to-poach-or-log", MapSize = 100, MaxSeconds = 90, Mods = "ludeon.rimworld.odyssey")]
        static void GuildDoesNotPoachOrLog(ScenarioContext s) {
            var guild = IncidentHelpers.FactionOf("TradersGuild");
            s.Check(guild != null, "the Odyssey world should hold the traders guild");
            IncidentHelpers.MakeHostile("TradersGuild");
            s.Check(guild.HostileTo(Faction.OfPlayer) && !guild.Hidden && guild.def.humanlikeFaction, "the guild should be hostile, visible and humanlike for the check");
            s.Check(guild.def.arrivalModeWhitelist != null && !guild.def.arrivalModeWhitelist.Contains(PawnsArrivalModeDefOf.EdgeWalkIn), "the guild should arrive by drop only, so it cannot walk in the way these two incidents arrive");
            IncidentHelpers.SpawnWildAnimal(s, "Muffalo", s.Cell(8, 8));
            IncidentHelpers.SpawnTreesWhereFree(s, 12, -10);
            var logBefore = IncidentHelpers.ModLogCount();
            var others = Find.FactionManager.AllFactions.Where(f => f != guild && !f.Hidden && !f.IsPlayer).ToList();
            foreach (var f in others) f.hidden = true;
            foreach (var incident in new[] { "SrPoaching", "SrLogging" }) {
                var def = DefDatabase<IncidentDef>.GetNamed(incident);
                s.Check(!def.Worker.CanFireNow(new IncidentParms { target = s.Map, points = 1500f }), incident + " should not be able to fire when the guild, which cannot arrive on foot, is the only hostile faction");
            }
            foreach (var f in others) f.hidden = null;
            IncidentHelpers.MakeHostile("OutlanderRough");
            foreach (var incident in new[] { "SrPoaching", "SrLogging" }) {
                var control = IncidentHelpers.Fire(s, incident, null, 1500f);
                s.Check(control.Fired && control.Pawns.Count > 0 && control.Pawns.All(p => p.Faction != guild), incident + " should still bring a hostile faction that can walk in; got " + control.PawnSummary());
                IncidentHelpers.ClearGroup(s, control);
            }
            IncidentHelpers.CheckModLogQuiet(s, logBefore, "when the guild is not picked");
            s.RequireNoLogErrors();
        }

        static void RunAll(ScenarioContext s, string dlc) {
            var loaded = new[] { ("Royalty", ModsConfig.RoyaltyActive), ("Anomaly", ModsConfig.AnomalyActive), ("Biotech", ModsConfig.BiotechActive), ("Ideology", ModsConfig.IdeologyActive), ("Odyssey", ModsConfig.OdysseyActive) };
            if (dlc == "all five") s.AreEqual("Royalty Anomaly Biotech Ideology Odyssey", string.Join(" ", loaded.Where(o => o.Item2).Select(o => o.Item1)), "the DLC loaded in the combined launch");
            else s.AreEqual(dlc, string.Join(" ", loaded.Where(o => o.Item2).Select(o => o.Item1)), "the only DLC loaded in the " + dlc + " launch");
            var pirates = IncidentHelpers.EnsureFaction("Pirate");
            var outlanders = IncidentHelpers.EnsureFaction("OutlanderRough");
            IncidentHelpers.MakeHostile("OutlanderRough");
            IncidentHelpers.RunPoaching(s, dlc, pirates);
            IncidentHelpers.RunLogging(s, dlc, pirates);
            IncidentHelpers.RunCaravan(s, dlc, outlanders);
            IncidentHelpers.RunTraveler(s, dlc, pirates);
            var sent = new System.Collections.Generic.List<string>();
            foreach (var entry in DlcFactions.Where(f => dlc == "all five" || f.Dlc == dlc)) RunDlcFaction(s, entry, sent);
            s.Metric("dlc_faction_incidents", sent.Count);
            s.AreEqual(ExpectedSent(dlc), string.Join(" ", sent), "the DLC faction incidents that ran");
            s.RequireNoLogErrors();
            s.RequireNoPlayerLogErrors();
        }

        static string ExpectedSent(string dlc) {
            var royalty = "Empire:SrPoaching Empire:SrLogging Empire:SrHositleTraveler";
            string Full(string f) => f + ":SrPoaching " + f + ":SrLogging " + f + ":SrHositleCaravanPassing " + f + ":SrHositleTraveler";
            string Pirates(string f) => f + ":SrPoaching " + f + ":SrLogging " + f + ":SrHositleTraveler";
            var biotech = string.Join(" ", Full("TribeRoughNeanderthal"), Pirates("PirateYttakin"), Full("TribeSavageImpid"), Full("OutlanderRoughPig"), Pirates("PirateWaster"));
            var ideology = string.Join(" ", Full("TribeCannibal"), Full("NudistTribe"), Pirates("CannibalPirate"));
            switch (dlc) {
                case "Royalty": return royalty;
                case "Biotech": return biotech;
                case "Ideology": return ideology;
                case "all five": return royalty + " " + biotech + " " + ideology;
                default: return "";
            }
        }

        static void RunDlcFaction(ScenarioContext s, (string Dlc, string Faction, string Xenotype) entry, System.Collections.Generic.List<string> sent) {
            var faction = IncidentHelpers.EnsureFaction(entry.Faction);
            var def = faction.def;
            s.Check(!faction.Hidden && def.humanlikeFaction, entry.Faction + " should be a visible humanlike faction");
            IncidentHelpers.MakeHostile(entry.Faction);
            s.Check(faction.HostileTo(Faction.OfPlayer), entry.Faction + " should be hostile after the goodwill drop");
            var label = entry.Dlc + "/" + entry.Faction;
            var seenXenotypes = new System.Collections.Generic.HashSet<string>();
            void Note(string incident, IncidentRun run) {
                sent.Add(entry.Faction + ":" + incident);
                foreach (var pawn in run.Pawns.Where(p => p.genes?.Xenotype != null)) seenXenotypes.Add(pawn.genes.Xenotype.defName);
            }
            var traveler = def.pawnGroupMakers.Any(m => m.kindDef == PawnGroupKindDefOf.Combat);
            var caravan = entry.Faction != "Empire" && def.caravanTraderKinds.Count > 0 && def.pawnGroupMakers.Any(m => m.kindDef == PawnGroupKindDefOf.Trader);
            if (entry.Faction == "Empire") {
                var refused = IncidentHelpers.Fire(s, "SrHositleCaravanPassing", faction, -1f);
                s.Check(!refused.Fired && refused.Pawns.Count == 0, label + ": the Empire caravan needs a colonist with the trading permit, as the game's own caravan does, so it should not come without one; fired=" + refused.Fired + " pawns=" + refused.Pawns.Count);
            }
            Note("SrPoaching", IncidentHelpers.RunPoaching(s, label, faction));
            Note("SrLogging", IncidentHelpers.RunLogging(s, label, faction));
            if (caravan) Note("SrHositleCaravanPassing", IncidentHelpers.RunCaravan(s, label, faction));
            if (traveler) Note("SrHositleTraveler", IncidentHelpers.RunTraveler(s, label, faction));
            if (entry.Xenotype != null) s.Check(seenXenotypes.Contains(entry.Xenotype), label + ": the pawns should include the " + entry.Xenotype + " xenotype the faction generates; seen: " + string.Join(", ", seenXenotypes));
        }
    }
}
