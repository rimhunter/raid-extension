using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using RimWorld;
using Verse;
using static RimhunterTestRunner.Scenarios.TranslationSupport;

namespace RimhunterTestRunner.Scenarios
{
    static class Translations
    {
        static readonly string[] KeyedKeys = { "SrTakeWoodExit", "SrTakePreyExit", "SrHostileTraderCaravanPassing" };

        static readonly (string DefType, string Entry)[] DefEntries = {
            ("IncidentDef", "SrPoaching.label"), ("IncidentDef", "SrLogging.label"),
            ("IncidentDef", "SrHositleTraveler.label"), ("IncidentDef", "SrHositleCaravanPassing.label"),
            ("JobDef", "SrTakeWoodExit.reportString"),
            ("RaidStrategyDef", "SrLogging.letterLabelEnemy"), ("RaidStrategyDef", "SrLogging.arrivalTextEnemy"),
            ("RaidStrategyDef", "SrPoaching.letterLabelEnemy"), ("RaidStrategyDef", "SrPoaching.arrivalTextEnemy"),
        };

        static XmlElement[] Entries(string path) {
            var doc = new XmlDocument();
            doc.Load(path);
            return doc.DocumentElement.ChildNodes.OfType<XmlElement>().ToArray();
        }

        static List<string> KeysNotLoaded(LoadedLanguage lang, IEnumerable<string> keys) =>
            keys.Where(k => !lang.keyedReplacements.ContainsKey(k)).ToList();

        static List<string> InjectionsNotLoaded(LoadedLanguage lang, IEnumerable<(string DefType, string Entry)> entries) =>
            entries.Where(e => {
                var package = lang.defInjections.SingleOrDefault(p => p.defType.Name == e.DefType);
                return package == null || !package.injections.ContainsKey(e.Entry);
            }).Select(e => e.DefType + "/" + e.Entry).ToList();

        [Scenario("every-existing-translation-loads-with-no-error", MapSize = 30, MaxSeconds = 120)]
        static void EveryTranslationLoads(ScenarioContext s) {
            var mod = ModLister.GetActiveModWithIdentifier(OurPackageId, ignorePostfix: true);
            if (mod == null) throw new ScenarioFailure("the mod " + OurPackageId + " is not active");
            var root = mod.RootDir.FullName;
            var englishKeyed = ModKeyed("English").Keys.ToList();
            s.AreEqual(3, englishKeyed.Count, "the original ships 3 English Keyed keys");
            s.AreEqual(9, DefEntries.Count(e => ModDefInjected("English", e.DefType).ContainsKey(e.Entry)), "the original ships 9 English DefInjected entries, the ones this scenario lists");
            foreach (var folder in Folders) {
                var lang = LanguageNamed(folder);
                lang.LoadData();
                s.Check(lang.loadErrors.Count == 0, folder + " loaded with errors: " + string.Join(" | ", lang.loadErrors));
                s.Check(!lang.anyKeyedReplacementsXmlParseError, folder + " has a Keyed file that does not parse: " + lang.lastKeyedReplacementsXmlParseErrorInFile);
                s.Check(!lang.anyDefInjectionsXmlParseError, folder + " has a DefInjected file that does not parse: " + lang.lastDefInjectionsXmlParseErrorInFile);

                var ownKeyed = ModKeyed(folder);
                s.AreEqual("", string.Join(", ", englishKeyed.Where(k => !ownKeyed.ContainsKey(k))), folder + ": English keys with no text in this language's Keyed files");
                s.AreEqual("", string.Join(", ", KeysNotLoaded(lang, englishKeyed)), folder + ": English keys the game did not load from this language");
                foreach (var file in Directory.GetFiles(Path.Combine(root, "Languages", folder, "Keyed"), "*.xml"))
                foreach (var e in Entries(file))
                    s.Check(lang.keyedReplacements.TryGetValue(e.Name, out var r) && r.value == e.InnerText.Replace("\\n", "\n"),
                        folder + " did not load the key " + e.Name + " from " + Path.GetFileName(file));

                var ownInjected = DefEntries.Where(e => ModDefInjected(folder, e.DefType).ContainsKey(e.Entry)).ToList();
                s.AreEqual("", string.Join(", ", DefEntries.Except(ownInjected).Select(e => e.DefType + "/" + e.Entry)), folder + ": English DefInjected entries with no text in this language's DefInjected files");
                s.AreEqual("", string.Join(", ", InjectionsNotLoaded(lang, DefEntries)), folder + ": DefInjected entries the game did not load from this language");
                foreach (var typeFolder in Directory.GetDirectories(Path.Combine(root, "Languages", folder, "DefInjected"))) {
                    var typeName = Path.GetFileName(typeFolder);
                    var package = lang.defInjections.SingleOrDefault(p => p.defType.Name == typeName);
                    s.Check(package != null, folder + " has the DefInjected folder " + typeName + ", but the game holds no injections for that def type");
                    if (package == null) continue;
                    foreach (var file in Directory.GetFiles(typeFolder, "*.xml", SearchOption.AllDirectories))
                    foreach (var e in Entries(file)) {
                        DefInjectionPackage.DefInjection injection = null;
                        s.Check(package.injections.TryGetValue(e.Name, out injection) && injection.injection == e.InnerText,
                            folder + " did not load the DefInjected key " + typeName + "/" + e.Name + "; the game holds: " + (injection == null ? "nothing" : injection.injection));
                        s.Check(GenDefDatabase.GetDefSilentFail(package.defType, e.Name.Split('.')[0], false) != null, folder + " translates " + typeName + " " + e.Name.Split('.')[0] + ", which no def has");
                    }
                }
            }
            s.RequireNoLogErrors();
        }

        static List<string> EnglishProblems() {
            var problems = new List<string>();
            var english = LanguageNamed("English");
            foreach (var key in KeyedKeys) {
                var shown = key.Translate("AAA", "BBB").ToString();
                var expected = OwnKeyed(english, key).Replace("{0}", "AAA").Replace("{1}", "BBB");
                if (shown != expected) problems.Add("key " + key + " draws '" + shown + "', the English file holds '" + expected + "'");
                if (shown.IndexOf("hositle", System.StringComparison.OrdinalIgnoreCase) >= 0) problems.Add("key " + key + " draws the hositle typo: '" + shown + "'");
            }
            foreach (var name in new[] { "SrPoaching", "SrLogging", "SrHositleTraveler", "SrHositleCaravanPassing" }) {
                var label = DefDatabase<IncidentDef>.GetNamed(name).label;
                if (label != OwnDef(english, "IncidentDef", name + ".label")) problems.Add("incident " + name + " label '" + label + "' differs from the English file");
                if (label.IndexOf("hositle", System.StringComparison.OrdinalIgnoreCase) >= 0) problems.Add("incident " + name + " label draws the hositle typo: '" + label + "'");
            }
            var report = DefDatabase<JobDef>.GetNamed("SrTakeWoodExit").reportString;
            if (report != OwnDef(english, "JobDef", "SrTakeWoodExit.reportString")) problems.Add("job SrTakeWoodExit report string '" + report + "' differs from the English file");
            foreach (var name in new[] { "SrPoaching", "SrLogging" }) {
                var strategy = DefDatabase<RaidStrategyDef>.GetNamed(name);
                if (strategy.letterLabelEnemy != OwnDef(english, "RaidStrategyDef", name + ".letterLabelEnemy")) problems.Add("strategy " + name + " letter label '" + strategy.letterLabelEnemy + "' differs from the English file");
                if (strategy.arrivalTextEnemy != OwnDef(english, "RaidStrategyDef", name + ".arrivalTextEnemy")) problems.Add("strategy " + name + " arrival text '" + strategy.arrivalTextEnemy + "' differs from the English file");
            }
            return problems;
        }

        [Scenario("translation-english-keys-and-def-text-draw-their-english-text", MapSize = 80, MaxSeconds = 120)]
        static void EnglishDrawsEnglish(ScenarioContext s) {
            s.Check(LanguageDatabase.activeLanguage == LanguageDatabase.defaultLanguage, "this scenario runs in English; the active language is " + LanguageDatabase.activeLanguage.folderName);
            s.AreEqual("", string.Join(" | ", EnglishProblems()), "English text drawn by the running game for the mod's keys and def fields");

            var outlanders = IncidentHelpers.FactionOf("OutlanderRough");
            IncidentHelpers.MakeHostile("OutlanderRough");
            var caravan = IncidentHelpers.Fire(s, "SrHositleCaravanPassing", outlanders, -1f);
            IncidentHelpers.CheckGroup(s, caravan, "SrHositleCaravanPassing", outlanders);
            var label = caravan.Letters.FirstOrDefault()?.Label.ToString();
            var expected = OwnKeyed(LanguageNamed("English"), "SrHostileTraderCaravanPassing").CapitalizeFirst();
            s.Check(label != null && label.StartsWith(expected), "the caravan letter label should start with '" + expected + "' (the English file); got '" + label + "'");
            s.Check(label != null && label.IndexOf("hositle", System.StringComparison.OrdinalIgnoreCase) < 0, "the caravan letter label should not hold the hositle typo; got '" + label + "'");
            IncidentHelpers.ClearGroup(s, caravan);
            s.RequireNoLogErrors();
        }

        [Scenario("translation-the-english-check-sees-a-typo-in-a-key-and-in-a-def-label", MapSize = 30, MaxSeconds = 60)]
        static void EnglishCheckPositiveControl(ScenarioContext s) {
            s.AreEqual("", string.Join(" | ", EnglishProblems()), "before the control, the English check finds nothing");
            var english = LanguageNamed("English");
            var key = "SrHostileTraderCaravanPassing";
            var original = english.keyedReplacements[key].value;
            var def = DefDatabase<IncidentDef>.GetNamed("SrHositleCaravanPassing");
            var label = def.label;
            english.keyedReplacements[key].value = original.Replace("Hostile", "Hositle");
            def.label = label.Replace("Hostile", "Hositle");
            try {
                var problems = EnglishProblems();
                s.Check(problems.Any(p => p.StartsWith("key " + key) && p.Contains("hositle typo")), "the English check should flag the typo in the key; found: " + string.Join(" | ", problems));
                s.Check(problems.Any(p => p.StartsWith("incident SrHositleCaravanPassing") && p.Contains("hositle typo")), "the English check should flag the typo in the incident label; found: " + string.Join(" | ", problems));
            } finally {
                english.keyedReplacements[key].value = original;
                def.label = label;
            }
            s.AreEqual("", string.Join(" | ", EnglishProblems()), "after the control, the English check finds nothing again");
            s.RequireNoLogErrors();
        }

        [Scenario("translation-the-load-check-sees-a-missing-key-and-a-missing-def-text", MapSize = 30, MaxSeconds = 60)]
        static void LoadCheckPositiveControl(ScenarioContext s) {
            var lang = LanguageNamed("Russian");
            lang.LoadData();
            s.AreEqual("", string.Join(", ", KeysNotLoaded(lang, KeyedKeys)), "before the control, Russian holds every key");
            s.AreEqual("", string.Join(", ", InjectionsNotLoaded(lang, DefEntries)), "before the control, Russian holds every DefInjected entry");

            var key = "SrTakeWoodExit";
            var keyed = lang.keyedReplacements[key];
            lang.keyedReplacements.Remove(key);
            var entry = ("RaidStrategyDef", "SrPoaching.letterLabelEnemy");
            var package = lang.defInjections.Single(p => p.defType.Name == entry.Item1);
            var injection = package.injections[entry.Item2];
            package.injections.Remove(entry.Item2);
            try {
                s.AreEqual(key, string.Join(", ", KeysNotLoaded(lang, KeyedKeys)), "the load check should name the key taken out");
                s.AreEqual(entry.Item1 + "/" + entry.Item2, string.Join(", ", InjectionsNotLoaded(lang, DefEntries)), "the load check should name the DefInjected entry taken out");
            } finally {
                lang.keyedReplacements[key] = keyed;
                package.injections[entry.Item2] = injection;
            }
            s.AreEqual("", string.Join(", ", KeysNotLoaded(lang, KeyedKeys)), "after the control, the key is back");
            s.AreEqual("", string.Join(", ", InjectionsNotLoaded(lang, DefEntries)), "after the control, the entry is back");
            s.RequireNoLogErrors();
        }

        [Scenario("translation-booted-language-letters-labels-and-keys-come-from-the-language-files", MapSize = 80, MaxSeconds = 150, SelfTest = true)]
        static void BootedLanguage(ScenarioContext s) {
            var lang = LanguageDatabase.activeLanguage;
            if (lang == LanguageDatabase.defaultLanguage) throw new ScenarioFailure("the game must be started in a language other than English: run scripts/test-translations, which passes --language");
            var folder = Legacy(lang.folderName);
            s.Check(Folders.Contains(folder), "the active language " + lang.folderName + " is not one of the original's nine");

            foreach (var key in KeyedKeys) {
                var shown = key.Translate("AAA", "BBB").ToString();
                s.Check(!LooksLikeMissingKey(shown), folder + " draws a missing-key marker for " + key + ": " + shown);
                var expected = OwnKeyed(lang, key).Replace("{0}", "AAA").Replace("{1}", "BBB");
                if (folder == "Korean") expected = expected.Replace("(이)가", "가");
                s.AreEqual(expected, shown, folder + " text for the key " + key + " (read from the language's own file on disk)");
            }

            var incidents = new[] { "SrPoaching", "SrLogging", "SrHositleTraveler", "SrHositleCaravanPassing" };
            foreach (var name in incidents)
                s.AreEqual(OwnDef(lang, "IncidentDef", name + ".label"), DefDatabase<IncidentDef>.GetNamed(name).label, folder + " label of the incident " + name);
            s.AreEqual(OwnDef(lang, "JobDef", "SrTakeWoodExit.reportString"), DefDatabase<JobDef>.GetNamed("SrTakeWoodExit").reportString, folder + " report string of the job SrTakeWoodExit");
            foreach (var name in new[] { "SrPoaching", "SrLogging" }) {
                var strategy = DefDatabase<RaidStrategyDef>.GetNamed(name);
                s.AreEqual(OwnDef(lang, "RaidStrategyDef", name + ".letterLabelEnemy"), strategy.letterLabelEnemy, folder + " letter label of the strategy " + name);
                s.AreEqual(OwnDef(lang, "RaidStrategyDef", name + ".arrivalTextEnemy"), strategy.arrivalTextEnemy, folder + " arrival text of the strategy " + name);
            }

            var pirates = IncidentHelpers.FactionOf("Pirate");
            IncidentHelpers.SpawnWildAnimal(s, "Muffalo", s.Cell(0, 0));
            for (var i = 0; i < 6; i++) IncidentHelpers.SpawnTree(s, s.Cell(-6 + 3 * (i % 3), -4 + 3 * (i / 3)));
            foreach (var pair in new[] { ("SrPoaching", "SrPoaching"), ("SrLogging", "SrLogging") }) {
                var run = IncidentHelpers.Fire(s, pair.Item1, pirates, 1500f);
                IncidentHelpers.CheckGroup(s, run, pair.Item1, pirates);
                var letter = run.Letters.FirstOrDefault() as ChoiceLetter;
                s.AreEqual(1, run.Letters.Count, folder + ": one letter for " + pair.Item1 + "; letters: " + run.LetterSummary());
                if (letter == null) { s.Fail(folder + ": the letter for " + pair.Item1 + " is not a standard letter"); IncidentHelpers.ClearGroup(s, run); continue; }
                var label = OwnDef(lang, "RaidStrategyDef", pair.Item2 + ".letterLabelEnemy");
                var text = OwnDef(lang, "RaidStrategyDef", pair.Item2 + ".arrivalTextEnemy");
                s.Check(letter.Label.ToString().StartsWith(label) && letter.Label.ToString().Contains(pirates.Name), folder + ": the " + pair.Item1 + " letter label should start with '" + label + "' and name " + pirates.Name + "; got '" + letter.Label.ToString() + "'");
                s.Check(letter.Text.ToString().Contains(text), folder + ": the " + pair.Item1 + " letter text should hold '" + text + "'; got '" + letter.Text.ToString() + "'");
                s.Check(!LooksLikeMissingKey(letter.Label.ToString()) && !LooksLikeMissingKey(letter.Text.ToString()), folder + ": the " + pair.Item1 + " letter shows a missing-key marker");
                IncidentHelpers.ClearGroup(s, run);
            }

            var outlanders = IncidentHelpers.FactionOf("OutlanderRough");
            IncidentHelpers.MakeHostile("OutlanderRough");
            var caravan = IncidentHelpers.Fire(s, "SrHositleCaravanPassing", outlanders, -1f);
            IncidentHelpers.CheckGroup(s, caravan, "SrHositleCaravanPassing", outlanders);
            var caravanLetter = caravan.Letters.FirstOrDefault();
            s.AreEqual(1, caravan.Letters.Count, folder + ": one caravan letter; letters: " + caravan.LetterSummary());
            var expectedCaravan = OwnKeyed(lang, "SrHostileTraderCaravanPassing").CapitalizeFirst();
            s.Check(caravanLetter != null && caravanLetter.Label.ToString().StartsWith(expectedCaravan), folder + ": the caravan letter label should start with '" + expectedCaravan + "' (the language's own file on disk; the game may add a relationship suffix); got '" + caravanLetter?.Label.ToString() + "'");
            IncidentHelpers.ClearGroup(s, caravan);
            s.RequireNoLogErrors();
        }
    }
}
