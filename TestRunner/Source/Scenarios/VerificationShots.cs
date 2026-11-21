using System;
using System.Collections;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimhunterTestRunner.Scenarios
{
    static class VerificationShots
    {
        const int CellsAcross = 40;

        static bool OnScreen(ScenarioContext s, IntVec3 cell) {
            var p = s.ScreenPixelOf(cell);
            return p.x > 40f && p.y > 40f && p.x < UnityEngine.Screen.width - 40f && p.y < UnityEngine.Screen.height - 120f;
        }

        static IEnumerator PlaceCamera(ScenarioContext s, IntVec3 cell, string name, int across) {
            CameraJumper.TryHideWorld();
            for (var i = 0; i < 300 && WorldRendererUtility.WorldRendered; i++) yield return null;
            s.Check(!WorldRendererUtility.WorldRendered, name + ": the world view should be closed before the camera is placed");
            var placed = false;
            for (var attempt = 0; attempt < 10 && !placed; attempt++) {
                s.LookAt(cell, across);
                yield return ScenarioContext.Frames(5);
                s.LookAt(cell, across);
                yield return ScenarioContext.Frames(5);
                placed = OnScreen(s, cell);
            }
            s.Check(placed, name + ": the camera should show " + cell + " after being placed on it; it shows " + Find.CameraDriver.MapPosition);
        }

        static void CloseWindows() {
            foreach (var window in Find.WindowStack.Windows.ToList()) Find.WindowStack.TryRemove(window, doCloseSound: false);
        }

        static void ClearOlderLetters() {
            foreach (var letter in Find.LetterStack.LettersListForReading.ToList()) Find.LetterStack.RemoveLetter(letter);
        }

        static string Head(string text, int length) => text.Substring(0, Math.Min(length, text.Length));

        static IEnumerator LetterShots(ScenarioContext s, IncidentRun run, string name) {
            var letter = run.Letters.FirstOrDefault() as ChoiceLetter;
            s.Check(letter != null, name + ": the incident should have shown a standard letter; letters: " + run.LetterSummary());
            if (letter == null) yield break;
            var label = Head(letter.Label.ToString(), 14);
            var text = Head(letter.Text.ToString().StripTags(), 14);
            var stack = s.Screenshot(name + "-letter-in-the-letter-stack");
            yield return stack;
            s.Check(stack.ShowsText(label), name + ": the letter stack should draw '" + label + "'; drawn: " + stack.TextsMatching(label));
            letter.OpenLetter();
            yield return ScenarioContext.Frames(5);
            var opened = s.Screenshot(name + "-letter-opened");
            yield return opened;
            s.Check(opened.ShowsText(text), name + ": the opened letter should draw its text starting '" + text + "'; drawn: " + opened.TextsMatching(text));
            CloseWindows();
            yield return ScenarioContext.Frames(5);
        }

        static IEnumerator MessageShot(ScenarioContext s, IncidentRun run, string name) {
            var message = run.Messages.FirstOrDefault();
            s.Check(message != null, name + ": the incident should have shown a message; messages: " + run.MessageSummary());
            if (message == null) yield break;
            var head = Head(message.text, 24);
            var shot = s.Screenshot(name + "-message-on-arrival");
            yield return shot;
            s.Check(shot.ShowsText(head), name + ": the screen should draw the message starting '" + head + "'; drawn: " + shot.TextsMatching(head));
        }

        static IEnumerator InProgressShot(ScenarioContext s, IncidentRun run, string name, IntVec3 center, Func<ScenarioContext, bool> shownOnScreen, string what, int across = CellsAcross) {
            var edgeScroll = Prefs.EdgeScreenScroll;
            Prefs.EdgeScreenScroll = false;
            try {
                yield return PlaceCamera(s, center, name, across);
                var shot = s.Screenshot(name + "-in-progress");
                yield return shot;
                var onScreen = run.Pawns.Count(p => p.Spawned && OnScreen(s, p.Position));
                s.Metric(name + ": group pawns on screen", onScreen);
                s.Check(onScreen >= 1, name + ": at least one pawn of the group should be on screen; on screen: " + onScreen + "; camera at " + Find.CameraDriver.MapPosition + ", wanted " + center);
                s.Check(shownOnScreen(s), name + ": " + what + " should be on screen");
            } finally {
                Prefs.EdgeScreenScroll = edgeScroll;
            }
        }

        static IntVec3 Centroid(IncidentRun run) {
            var spawned = run.Pawns.Where(p => p.Spawned).ToList();
            return new IntVec3((int) spawned.Average(p => p.Position.x), 0, (int) spawned.Average(p => p.Position.z));
        }

        [Scenario("screenshot-poaching-letter-and-hunt-in-progress", MapSize = 100, Windowed = true, MaxSeconds = 150)]
        static IEnumerator Poaching(ScenarioContext s) {
            ClearOlderLetters();
            var pirates = IncidentHelpers.FactionOf("Pirate");
            var animal = IncidentHelpers.SpawnWildAnimal(s, "Muffalo", s.Cell(0, 0));
            var run = IncidentHelpers.Fire(s, "SrPoaching", pirates, 1500f);
            IncidentHelpers.CheckGroup(s, run, "SrPoaching", pirates);
            yield return LetterShots(s, run, "poaching");
            var minimum = float.MaxValue;
            var chasing = false;
            for (var i = 0; i < 200 && !chasing && !animal.Dead; i++) {
                s.Tick(30);
                var duty = run.Pawns.Where(p => p.Spawned && IncidentHelpers.DutyOf(p) == "SrPoaching").ToList();
                if (duty.Count == 0 || !animal.Spawned) continue;
                minimum = Math.Min(minimum, duty.Min(p => p.Position.DistanceTo(animal.Position)));
                chasing = minimum <= 26f;
            }
            s.Check(chasing && !animal.Dead, "the poachers should be within 26 cells of the living animal when the shot is taken; closest seen " + minimum + ", animal dead " + animal.Dead + ", animal spawned " + animal.Spawned);
            if (!chasing) yield break;
            var near = run.Pawns.Where(p => p.Spawned).OrderBy(p => p.Position.DistanceTo(animal.Position)).First();
            var middle = new IntVec3((near.Position.x + animal.Position.x) / 2, 0, (near.Position.z + animal.Position.z) / 2);
            yield return InProgressShot(s, run, "poaching", middle, c => animal.Spawned && OnScreen(c, animal.Position), "the hunted animal", 70);
            s.RequireNoLogErrors();
        }

        [Scenario("screenshot-logging-letter-and-felling-in-progress", MapSize = 100, Windowed = true, MaxSeconds = 150)]
        static IEnumerator Logging(ScenarioContext s) {
            ClearOlderLetters();
            var pirates = IncidentHelpers.FactionOf("Pirate");
            for (var i = 0; i < 40; i++) IncidentHelpers.SpawnTree(s, s.Cell(-13 + 3 * (i % 10), -9 + 3 * (i / 10)));
            var treesBefore = IncidentHelpers.Trees(s.Map);
            var run = IncidentHelpers.Fire(s, "SrLogging", pirates, 1500f);
            IncidentHelpers.CheckGroup(s, run, "SrLogging", pirates);
            foreach (var pawn in run.Pawns.Where(p => !p.skills.GetSkill(SkillDefOf.Plants).TotallyDisabled)) pawn.skills.GetSkill(SkillDefOf.Plants).Level = 12;
            yield return LetterShots(s, run, "logging");
            var cutting = s.RunUntil(() => IncidentHelpers.Trees(s.Map) < treesBefore && run.Pawns.Any(p => p.Spawned && p.CurJobDef == JobDefOf.CutPlant), 15000);
            s.Check(cutting, "a logger should be cutting a tree near the middle of the map; " + IncidentHelpers.Dump(s, run));
            if (!cutting) yield break;
            yield return InProgressShot(s, run, "logging", s.Cell(0, -4), c => c.Map.listerThings.AllThings.Any(t => t is Plant plant && plant.def.plant.IsTree && plant.Spawned && OnScreen(c, plant.Position)), "a tree");
            s.RequireNoLogErrors();
        }

        [Scenario("screenshot-hostile-caravan-letter-and-caravan-in-progress", MapSize = 100, Windowed = true, MaxSeconds = 150)]
        static IEnumerator Caravan(ScenarioContext s) {
            ClearOlderLetters();
            var outlanders = IncidentHelpers.FactionOf("OutlanderRough");
            IncidentHelpers.MakeHostile("OutlanderRough");
            var run = IncidentHelpers.Fire(s, "SrHositleCaravanPassing", outlanders, -1f);
            IncidentHelpers.CheckGroup(s, run, "SrHositleCaravanPassing", outlanders);
            yield return LetterShots(s, run, "hostile-caravan");
            s.Tick(600);
            var spawned = run.Pawns.Where(p => p.Spawned).ToList();
            s.Check(spawned.Count >= 3, "the caravan should still be on the map 600 ticks after it arrived; on the map: " + spawned.Count);
            if (spawned.Count == 0) yield break;
            yield return InProgressShot(s, run, "hostile-caravan", Centroid(run), c => run.Pawns.Any(p => p.Spawned && p.TraderKind != null && OnScreen(c, p.Position)), "the trader");
            s.RequireNoLogErrors();
        }

        [Scenario("screenshot-hostile-traveler-message-and-group-in-progress", MapSize = 100, Windowed = true, MaxSeconds = 150)]
        static IEnumerator Traveler(ScenarioContext s) {
            ClearOlderLetters();
            var pirates = IncidentHelpers.FactionOf("Pirate");
            s.SpawnColonist(s.Cell(0, 0));
            var run = IncidentHelpers.Fire(s, "SrHositleTraveler", pirates, 1500f);
            IncidentHelpers.CheckGroup(s, run, "SrHositleTraveler", pirates);
            yield return MessageShot(s, run, "hostile-traveler");
            s.Tick(600);
            var spawned = run.Pawns.Where(p => p.Spawned).ToList();
            s.Check(spawned.Count >= 2, "the travelers should still be on the map 600 ticks after they arrived; on the map: " + spawned.Count);
            if (spawned.Count == 0) yield break;
            yield return InProgressShot(s, run, "hostile-traveler", Centroid(run), c => run.Pawns.Count(p => p.Spawned && OnScreen(c, p.Position)) >= 2, "at least two pawns of the group");
            s.RequireNoLogErrors();
        }
    }
}
