using System.Text.RegularExpressions;

namespace RaidExtension.Tests;

static class DrawnTextCheck
{
    const string Hole = @"\{[^{}]*\}";
    static readonly Regex Literal = new(
        @"\$?""""""[\s\S]*?""""""" +
        @"|(?:\$@|@\$?)""(?:[^""]|"""")*""" +
        @"|\$""(?:[^""\\{]|\\.|\{\{|" + Hole + @")*""" +
        @"|""(?:[^""\\]|\\.)*""");
    static readonly Regex Comment = new(@"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline);
    static readonly Regex Tokens = new(Comment + "|" + @"'(?:[^'\\]|\\.)'|" + Literal, RegexOptions.Singleline);
    static readonly Regex NotDrawnBefore = new(
        @"(?:\bLog\.\w+\(|\bnew\s+\w*Exception\(|\bTrigger_Memo\(|\.Named\(" +
        @"|\bconst\s+string\s+(?:LogTag|DefNameGauranlenTree)\s*=)\s*$" +
        @"|\bScribe_\w+\.Look\([^;]*$");
    static readonly Regex TwoLetters = new(@"\p{L}{2}");
    static readonly Regex KeyShape = new(@"^""[A-Za-z0-9_.]+""$");
    static readonly Regex Interpolation = new(Hole);
    static readonly Regex Escape = new(@"\\.");
    static readonly Regex Translated = new(@"^\s*\.(?:Translate|TryTranslate)\b");

    public static List<string> Find(string source) {
        var text = Tokens.Replace(source, m => m.Value.StartsWith("//") || m.Value.StartsWith("/*") ? Regex.Replace(m.Value, @"[^\n]", " ") : m.Value);
        var masked = Literal.Replace(text, m => new string('S', m.Length));
        var found = new List<string>();
        foreach (Match m in Literal.Matches(text)) {
            var literal = m.Value;
            var shown = literal.Contains('$') ? Interpolation.Replace(literal, "") : literal;
            if (!TwoLetters.IsMatch(Escape.Replace(shown, ""))) continue;
            if (KeyShape.IsMatch(literal) && Translated.IsMatch(text.Substring(m.Index + m.Length))) continue;
            var start = new[] { ';', '{', '}' }.Max(c => masked.LastIndexOf(c, m.Index));
            var before = masked.Substring(start + 1, m.Index - start - 1);
            if (NotDrawnBefore.IsMatch(before)) continue;
            found.Add("line " + (text.Take(m.Index).Count(c => c == '\n') + 1) + ": " + literal);
        }
        return found;
    }
}
