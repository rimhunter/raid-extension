using System.Diagnostics;
using System.Xml.Linq;
using Xunit;

namespace RaidExtension.Tests;

public class ModFolderTests
{
    static readonly string Root = ModRoot.Path;

    static XElement Load(string relative) => XDocument.Load(Path.Combine(Root, relative)).Root;

    static bool InBuildOutput(string path) {
        var sep = Path.DirectorySeparatorChar;
        return path.Contains($"{sep}.git{sep}") || path.Contains($"{sep}bin{sep}") || path.Contains($"{sep}obj{sep}");
    }

    static string Git(string arguments) {
        var git = Process.Start(new ProcessStartInfo("git", arguments) {
            WorkingDirectory = Root, RedirectStandardOutput = true, RedirectStandardError = true,
        });
        var output = git.StandardOutput.ReadToEnd();
        var error = git.StandardError.ReadToEnd();
        git.WaitForExit();
        Assert.True(git.ExitCode == 0, "git " + arguments + " failed: " + error);
        return output.Trim();
    }

    [Fact]
    public void About_supports_exactly_1_6_and_1_7() {
        var about = Load("About/About.xml");
        Assert.Equal(new[] { "1.6", "1.7" }, about.Element("supportedVersions").Elements("li").Select(l => l.Value));
    }

    [Fact]
    public void About_has_its_own_package_id() {
        Assert.Equal("rimhunter.raidextension", Load("About/About.xml").Element("packageId").Value);
    }

    [Fact]
    public void About_declares_no_dependency() {
        var about = Load("About/About.xml");
        Assert.Null(about.Element("modDependencies"));
        Assert.Null(about.Element("modDependenciesByVersion"));
    }

    [Fact]
    public void Load_folders_pick_the_matching_build_for_each_game_version() {
        var folders = Load("LoadFolders.xml");
        Assert.Equal(new[] { "/", "1.6" }, folders.Element("v1.6").Elements("li").Select(l => l.Value));
        Assert.Equal(new[] { "/", "1.7" }, folders.Element("v1.7").Elements("li").Select(l => l.Value));
    }

    [Fact]
    public void No_prebuilt_binary_is_committed() {
        Assert.Equal("", Git("ls-files -- \"*.dll\" \"*.pdb\" \"*.exe\""));
    }

    [Fact]
    public void The_old_per_version_folders_are_gone() {
        Assert.Equal("", Git("ls-files -- 1.2 1.3 1.4"));
    }

    [Fact]
    public void The_mod_project_references_only_the_game() {
        var csproj = Load("Source/ModRimWorldRaidExtension.csproj");
        Assert.Empty(csproj.Descendants("Reference"));
        var references = csproj.Descendants("PackageReference").Select(r => r.Attribute("Include").Value);
        Assert.Equal(new[] { "Krafs.Rimworld.Ref" }, references);
    }

    [Fact]
    public void Nothing_references_Harmony() {
        var offenders = Directory.GetFiles(Path.Combine(Root, "Source"), "*", SearchOption.AllDirectories)
            .Where(p => p.EndsWith(".cs") || p.EndsWith(".csproj"))
            .Where(p => !InBuildOutput(p))
            .Where(p => File.ReadAllText(p).Contains("Harmony"))
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void About_lists_both_original_package_ids_as_incompatible() {
        var ids = Load("About/About.xml").Element("incompatibleWith").Elements("li").Select(l => l.Value).OrderBy(v => v);
        Assert.Equal(new[] { "sr.modrimworld.raidextension", "sr.modrimworld.raidextensioncontinued" }, ids);
    }

    [Fact]
    public void No_Workshop_id_file_is_committed_until_this_mod_has_its_own() {
        Assert.Equal("", Git("ls-files -- \"*PublishedFileId.txt\""));
    }
}
