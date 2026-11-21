using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class TranslationSupport
    {
        public const string OurPackageId = "rimhunter.raidextension";

        public static readonly string[] Folders = { "ChineseSimplified", "ChineseTraditional", "English", "French", "German", "Japanese", "Korean", "PortugueseBrazilian", "Russian" };

        public static string Legacy(string folderName) {
            var bracket = folderName.IndexOf('(');
            return (bracket < 0 ? folderName : folderName.Substring(0, bracket)).Trim();
        }

        public static LoadedLanguage LanguageNamed(string folder) {
            var lang = LanguageDatabase.AllLoadedLanguages.SingleOrDefault(l => Legacy(l.folderName) == folder);
            if (lang == null) throw new ScenarioFailure("the game has no language for " + folder);
            return lang;
        }

        static ModContentPack Package(string packageId) {
            var pack = LoadedModManager.RunningModsListForReading.FirstOrDefault(m => string.Equals(m.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
            if (pack == null) throw new ScenarioFailure("the mod " + packageId + " is not running");
            return pack;
        }

        static string LanguagesDir() => Path.Combine(Package(OurPackageId).RootDir, "Languages");

        static void AddEntries(Dictionary<string, string> into, XmlDocument doc) {
            foreach (var e in doc.DocumentElement.ChildNodes.OfType<XmlElement>()) {
                var text = e.InnerText.Replace("\\n", "\n");
                if (text.Length > 0 && text != "TODO") into[e.Name] = text;
            }
        }

        static Dictionary<string, string> InFolder(string folder, string subFolder) {
            var texts = new Dictionary<string, string>();
            var dir = Path.Combine(folder, subFolder);
            if (!Directory.Exists(dir)) return texts;
            foreach (var file in Directory.GetFiles(dir, "*.xml", SearchOption.AllDirectories)) {
                var doc = new XmlDocument();
                doc.Load(file);
                AddEntries(texts, doc);
            }
            return texts;
        }

        public static Dictionary<string, string> ModKeyed(string legacyFolder) =>
            InFolder(Path.Combine(LanguagesDir(), legacyFolder), "Keyed");

        public static Dictionary<string, string> ModDefInjected(string legacyFolder, string defType) =>
            InFolder(Path.Combine(LanguagesDir(), legacyFolder, "DefInjected"), defType);

        public static string OwnKeyed(LoadedLanguage lang, string key) {
            if (ModKeyed(Legacy(lang.folderName)).TryGetValue(key, out var text)) return text;
            throw new ScenarioFailure("the " + lang.folderName + " Keyed files on disk hold no text for " + key + " (every language of the original ships every key)");
        }

        public static string OwnDef(LoadedLanguage lang, string defType, string entry) {
            if (ModDefInjected(Legacy(lang.folderName), defType).TryGetValue(entry, out var text)) return text;
            throw new ScenarioFailure("the " + lang.folderName + " DefInjected files on disk hold no " + defType + "/" + entry + " (every language of the original ships it)");
        }

        public static bool LooksLikeMissingKey(string drawn) =>
            System.Text.RegularExpressions.Regex.IsMatch(drawn, @"\bSr[A-Z][A-Za-z]+\b|Translation missing|\bTODO\b|NoTranslation|\u2e28|\u2e29");
    }
}
