using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace RaidExtension.Tests;

public class TranslationTests
{
    static readonly string Root = ModRoot.Path;

    static readonly string[] CoreKeys = { "LetterTraderCaravanArrival", "LetterRelatedPawnsNeutralGroup" };

    static readonly string[] DefTextFields = {
        "label", "labelShort", "description", "reportString", "arrivedLetter", "letterLabelEnemy",
        "arrivalTextEnemy", "textEnemy", "textFriendly", "textWillArrive", "jobReportString",
    };

    static readonly string[] Languages = {
        "ChineseSimplified", "ChineseTraditional", "English", "French", "German",
        "Japanese", "Korean", "PortugueseBrazilian", "Russian",
    };

    static IEnumerable<string> SourceFiles() =>
        Directory.GetFiles(Path.Combine(Root, "Source"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(p => p is "obj" or "bin" or "Lib"));

    static Dictionary<string, string> Entries(string language, string folder) {
        var dir = Path.Combine(Root, "Languages", language, folder);
        if (!Directory.Exists(dir)) return new Dictionary<string, string>();
        return Directory.GetFiles(dir, "*.xml", SearchOption.AllDirectories)
            .SelectMany(f => XDocument.Load(f).Root.Elements())
            .ToDictionary(e => e.Name.LocalName, e => e.Value);
    }

    static Dictionary<string, string> EnglishKeyed() => Entries("English", "Keyed");

    [Fact]
    public void Source_has_files_to_check() {
        Assert.True(SourceFiles().Count() >= 28, "the check found too few source files, so it would pass on nothing");
    }

    [Fact]
    public void No_code_file_passes_a_literal_with_letters_to_a_drawing_or_message_call() {
        var bad = SourceFiles().SelectMany(f => DrawnTextCheck.Find(File.ReadAllText(f)).Select(m => Path.GetRelativePath(Root, f) + " " + m)).ToList();
        Assert.True(bad.Count == 0, "English text written in code instead of a translation key: " + string.Join(" | ", bad));
    }

    [Theory]
    [InlineData("Messages.Message(\"Planted message\", MessageTypeDefOf.NeutralEvent);")]
    [InlineData("var l = LetterMaker.MakeLetter(\"Planted label\", \"Planted text\", LetterDefOf.NeutralEvent);")]
    [InlineData("Find.LetterStack.ReceiveLetter(\"Planted\", \"text\", LetterDefOf.NeutralEvent);")]
    [InlineData("SendStandardLetter(\"Planted label\", \"Planted text\", LetterDefOf.ThreatSmall, parms, pawns);")]
    [InlineData("var m = new TransitionAction_Message(\"Planted message\", MessageTypeDefOf.ThreatSmall);")]
    [InlineData("var m = new TransitionAction_Message($\"Planted {x}\", MessageTypeDefOf.ThreatSmall);")]
    [InlineData("var c = new Command_Action { defaultLabel = \"Planted gizmo\" };")]
    [InlineData("var o = new FloatMenuOption(\"Planted option\", null);")]
    [InlineData("TooltipHandler.TipRegion(rect, \"Planted tip\");")]
    [InlineData("ls.Label(\"Planted label\");")]
    [InlineData("ls.Label(\"Hello world\".Translate());")]
    [InlineData("ls.Label(\"Hello world\".TryTranslate(out var t));")]
    [InlineData("ls.Label(\"中文标签\");")]
    [InlineData("ls.Label(string.Format(\"Planted {0}\", x));")]
    [InlineData("ls.Label(\"Planted \" + x);")]
    [InlineData("ls.Label(x + \" planted\");")]
    [InlineData("const string Planted = \"Planted label\"; ls.Label(Planted);")]
    [InlineData("var p = @\"\\\"; ls.Label(\"Planted\");")]
    [InlineData("var p = \"\"\"a \" b\"\"\"; ls.Label(\"Planted\");")]
    [InlineData("var q = @\"say \"\"//\"\" ok\"; ls.Label(\"Planted\");")]
    [InlineData("var q = @\"a\\\"; var r = @\"b\\\"; ls.Label(@\"Planted\\\");")]
    [InlineData("ls.Label(@\"Planted\");")]
    [InlineData("ls.Label(@\"Planted \"\"quoted\"\" and a backslash \\\");")]
    [InlineData("ls.Label($@\"Planted {x}\");")]
    [InlineData("ls.Label(@$\"Planted {x}\");")]
    [InlineData("ls.Label(\"\"\"Planted\"\"\");")]
    [InlineData("ls.Label(\"\"\"\n    Planted \"quoted\"\n    \"\"\");")]
    [InlineData("ls.Label($\"\"\"Planted {x}\"\"\");")]
    [InlineData("var u = @\"//\"; ls.Label(\"Planted after a verbatim slash pair\");")]
    [InlineData("Messages.Message(\"Planted\".Named(x), MessageTypeDefOf.NeutralEvent);")]
    [InlineData("Messages.Message(\"Planted\" + x, MessageTypeDefOf.NeutralEvent); Log.Warning(\"a log line\");")]
    [InlineData("ls.Label(\"Planted\" + Log.Format(x));")]
    [InlineData("ls.Label(x.Named(\"LEADER\") + \"Planted\");")]
    [InlineData("MapGenerator.SetVar(\"Planted\", x);")]
    [InlineData("var d = new Dialog { tutorTag = \"Planted\" };")]
    [InlineData("Draw(\"Planted label\");")]
    [InlineData("var u = \"//\"; ls.Label(\"Planted after a slash pair\");")]
    [InlineData("ls.Label(\"Planted\"); /* \" */ ls.Label(\"Planted two\");")]
    public void A_planted_literal_is_rejected(string code) {
        Assert.NotEmpty(DrawnTextCheck.Find("class P {\nvoid M() {\n" + code + "\n}\n}"));
    }

    [Theory]
    [InlineData("var a = \"SrKey\".Translate();")]
    [InlineData("Messages.Message(\"SrKey\".Translate(x, y), MessageTypeDefOf.ThreatSmall);")]
    [InlineData("var l = \"SrKey\"\n    .Translate((NamedArgument) x).CapitalizeFirst();")]
    [InlineData("Log.Warning(\"a log line\");")]
    [InlineData("Log.Error($\"{MiscDef.LogTag}target must be a map.\");")]
    [InlineData("throw new InvalidOperationException(\"an error message\");")]
    [InlineData("Scribe_Values.Look(ref _point, \"_point\");")]
    [InlineData("Log.Warning(@\"a log line\");")]
    [InlineData("Log.Error($@\"{MiscDef.LogTag}a log line\");")]
    [InlineData("throw new InvalidOperationException(@\"an error message\");")]
    [InlineData("var t = new Trigger_Memo(\"TravelArrived\");")]
    [InlineData("public const string LogTag = \"[SR.Planted]\";")]
    [InlineData("public const string DefNameGauranlenTree = \"Plant_TreeGauranlen\";")]
    [InlineData("ls.Label($\"{\"SrKey\".Translate()}: {x}\");")]
    [InlineData("var s = x.ToString(\"F1\");")]
    [InlineData("// ls.Label(\"a comment\");")]
    [InlineData("/* ls.Label(\"a comment\"); */")]
    [InlineData("ls.Label(string.Format(\"SrKey\".Translate(), x));")]
    [InlineData("ls.Label(x.Named(\"LEADER\"));")]
    public void Keys_logs_errors_and_comments_are_accepted(string code) {
        Assert.Empty(DrawnTextCheck.Find("class P {\nvoid M() {\n" + code + "\n}\n}"));
    }

    [Fact]
    public void A_planted_literal_is_reported_with_its_line() {
        var found = DrawnTextCheck.Find("class P {\nvoid M() {\nls.Label(\"Planted\");\n}\n}");
        Assert.Equal(new[] { "line 3: \"Planted\"" }, found);
    }

    [Fact]
    public void Every_key_the_code_translates_has_an_English_text() {
        var english = EnglishKeyed();
        var used = SourceFiles().SelectMany(f => Regex.Matches(File.ReadAllText(f), "\"([A-Za-z0-9_.]+)\"\\s*\\.(?:Translate|TryTranslate)\\b").Select(m => m.Groups[1].Value)).ToHashSet();
        Assert.True(used.Count >= 5, "the check found too few translated keys: " + used.Count);
        var missing = used.Except(CoreKeys).Except(english.Keys).OrderBy(k => k).ToList();
        Assert.True(missing.Count == 0, "keys used in code with no English text: " + string.Join(", ", missing));
        Assert.True(english.All(p => p.Value.Trim().Length > 0), "an English key has no text");
    }

    [Fact]
    public void Every_def_text_has_an_English_DefInjected_entry() {
        var missing = new List<string>();
        var checkedFields = 0;
        foreach (var file in Directory.GetFiles(Path.Combine(Root, "Defs"), "*.xml", SearchOption.AllDirectories))
        foreach (var def in XDocument.Load(file).Root.Elements()) {
            var name = def.Element("defName")?.Value;
            if (name == null) continue;
            var injected = Entries("English", Path.Combine("DefInjected", def.Name.LocalName)).Keys.ToHashSet();
            foreach (var field in def.Elements().Where(e => DefTextFields.Contains(e.Name.LocalName) && Regex.IsMatch(e.Value, "[A-Za-z]{2}"))) {
                checkedFields++;
                if (!injected.Contains(name + "." + field.Name.LocalName)) missing.Add(def.Name.LocalName + " " + name + "." + field.Name.LocalName);
            }
        }
        Assert.True(checkedFields >= 9, "the check found too few def text fields: " + checkedFields);
        Assert.True(missing.Count == 0, "def text with no English DefInjected entry: " + string.Join(", ", missing));
    }

    [Fact]
    public void Every_def_text_equals_its_English_DefInjected_entry() {
        var different = new List<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(Root, "Defs"), "*.xml", SearchOption.AllDirectories))
        foreach (var def in XDocument.Load(file).Root.Elements()) {
            var name = def.Element("defName")?.Value;
            if (name == null) continue;
            var injected = Entries("English", Path.Combine("DefInjected", def.Name.LocalName));
            foreach (var field in def.Elements().Where(e => DefTextFields.Contains(e.Name.LocalName)))
                if (injected.TryGetValue(name + "." + field.Name.LocalName, out var text) && text != field.Value)
                    different.Add(name + "." + field.Name.LocalName + ": def '" + field.Value + "', English file '" + text + "'");
        }
        Assert.True(different.Count == 0, "def text that differs from its English DefInjected entry: " + string.Join(" | ", different));
    }

    [Fact]
    public void Every_language_folder_of_the_original_keeps_every_English_key() {
        Assert.Equal(Languages.OrderBy(l => l), Directory.GetDirectories(Path.Combine(Root, "Languages")).Select(Path.GetFileName).OrderBy(l => l));
        var missing = new List<string>();
        foreach (var folder in new[] { "Keyed", "DefInjected" }) {
            var english = Entries("English", folder).Keys;
            Assert.True(english.Count >= 3, "English " + folder + " holds too few entries: " + english.Count);
            foreach (var language in Languages.Where(l => l != "English")) {
                var own = Entries(language, folder);
                missing.AddRange(english.Where(k => !own.ContainsKey(k)).Select(k => language + " " + folder + " " + k));
                Assert.True(own.Values.All(v => v.Trim().Length > 0), language + " " + folder + " holds an empty text");
            }
        }
        Assert.True(missing.Count == 0, "keys missing from a language folder: " + string.Join(", ", missing));
    }

    [Fact]
    public void English_text_has_no_hositle_typo() {
        var texts = Entries("English", "Keyed").Concat(Entries("English", "DefInjected")).ToList();
        Assert.True(texts.Count >= 12, "the check found too few English texts: " + texts.Count);
        var bad = texts.Where(p => Regex.IsMatch(p.Value, "hositle", RegexOptions.IgnoreCase)).Select(p => p.Key).ToList();
        Assert.True(bad.Count == 0, "English text spells hostile as hositle: " + string.Join(", ", bad));
        foreach (var file in Directory.GetFiles(Path.Combine(Root, "Defs"), "*.xml", SearchOption.AllDirectories))
            foreach (var field in XDocument.Load(file).Root.Descendants().Where(e => DefTextFields.Contains(e.Name.LocalName)))
                Assert.DoesNotMatch("(?i)hositle", field.Value);
    }
}
