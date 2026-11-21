# Verification, RimWorld 1.6.4871

Every scenario of this mod was run in the real game on RimWorld 1.6 (`Version.txt`: 1.6.4871 rev598, Steam build 23969874) on 2026-11-20, through the shared scenario runner (a git submodule of the development repository, not part of this tree). The machine's Steam copy was switched to the 1.6 branch for this run and switched back to 1.7.5014 afterwards. Headless runs used `scripts/run-scenarios` and `scripts/run-all`; windowed runs used `scripts/run-scenarios --windowed --save-screenshots docs/verification/1.6 -s <name>` (1.6 needs no `WAYLAND_DISPLAY`; the runner uses X11 through Xwayland) on a 3840x2160 window. No scenario of this repo is limited to 1.7 (none carries `OnlyOn`), so all of them ran. Each full image is scaled to 1152 pixels wide by the runner. Every screenshot scenario asserts what the game drew before the image is kept.

## Screenshots

The same four scenarios as `docs/verification/1.7`, taken on 1.6; the files have the same names and show the same things:

| File | Scenario | What it shows |
|---|---|---|
| `poaching-letter-in-the-letter-stack`, `poaching-letter-opened` | screenshot-poaching-letter-and-hunt-in-progress | The Poaching letter in the stack, and opened |
| `poaching-in-progress` | same | Poachers gathered in the lower middle of the view and a living large animal (a muffalo) at the top; the poachers are walking toward it |
| `logging-letter-in-the-letter-stack`, `logging-letter-opened` | screenshot-logging-letter-and-felling-in-progress | The Logging letter in the stack, and opened |
| `logging-in-progress` | same | The crew among 40 oaks, standing at trees, with trees already felled in the rows they worked |
| `hostile-caravan-letter-in-the-letter-stack`, `hostile-caravan-letter-opened` | screenshot-hostile-caravan-letter-and-caravan-in-progress | The hostile caravan letter in the stack, and opened |
| `hostile-caravan-in-progress` | same | The caravan 600 ticks after it arrived |
| `hostile-traveler-message-on-arrival`, `hostile-traveler-in-progress` | screenshot-hostile-traveler-message-and-group-in-progress | The message "A group of travelers from <faction> are passing by." (the hostile traveler sends no letter), and the group 600 ticks after arrival |
| `01-mod-list-incompatibility-warning` | screenshot-mod-list-warns-when-the-original-raid-extension-is-also-enabled | The mod list with the original Raid Extension enabled: this mod is marked with a warning that names the original |

## Results

Date of every run: 2026-11-20. Build: 1.6.4871 rev598. "Runs" counts attempts with the final code.

| Command | Scenarios | Result |
|---|---|---|
| `scripts/run-scenarios --timeout 290` (launch 0, 20 scenarios) | the 19 scenarios other than the next row (caravan x3, every-existing-translation-loads-with-no-error, logging, mod-list-does-not-warn-when-nothing-else-is-enabled, poaching x5, startup-loads-mod-content-with-no-errors, translation x3, traveler, work-settings x3) | pass, 2 of 2 runs |
| same | work-settings-incident-pawns-have-initialized-work-settings | FAIL, 0 of 2 runs (#11) |
| `scripts/run-all --launches 1` | mod-list-warns-for-each-other-raid-extension-build | pass, 1 of 1 |
| `RIMHUNTER_FACTIONALWAR_SRC=<Factional War checkout> scripts/run-all --launches 2 --timeout 290` | together-with-factional-war-both-load-and-an-incident-of-each-runs-with-no-errors | FAIL, 0 of 2 runs |
| `scripts/run-all --launches 3` | dlc-anomaly-all-four-incidents-run | pass, 1 of 1 |
| `scripts/run-all --launches 4` | dlc-all-five-all-four-incidents-run | FAIL, 0 of 1 |
| `scripts/run-all --launches 5` | dlc-biotech-all-four-incidents-run | pass, 1 of 1 |
| `scripts/run-all --launches 6` | dlc-ideology-all-four-incidents-run | FAIL, 0 of 2 |
| `scripts/run-all --launches 7` | dlc-odyssey-all-four-incidents-run, odyssey-traders-guild-does-not-walk-in-as-hostile-travelers, odyssey-traders-guild-is-not-picked-to-poach-or-log | pass, 1 of 1 each |
| `scripts/run-all --launches 8` | dlc-royalty-all-four-incidents-run | pass 1 of 2, 1 fail |
| `scripts/test-translations --timeout 290` (launch 9) | translation-booted-language-letters-labels-and-keys-come-from-the-language-files in Chinese (Simplified), Chinese (Traditional), French, German, Japanese, Korean, Portuguese (Brazil), Russian | pass in each of the 8 languages, 1 of 1 each |
| windowed | screenshot-mod-list-warns-when-the-original-raid-extension-is-also-enabled | pass, 1 of 1 |
| windowed | the four screenshot scenarios above | pass, 2 of 2 runs each |

Failures, with their exact errors:
- `together-with-factional-war-...` (0 of 2): `types with the same full name in both mods' code: expected <> but was <Microsoft.CodeAnalysis.EmbeddedAttribute, System.Runtime.CompilerServices.RefSafetyRulesAttribute>`. Both names are attributes the compiler emits into each assembly that targets the 1.6 framework; the scenario passes on 1.7. Filed on the project's issue tracker as #12.
- `dlc-all-five-all-four-incidents-run` (0 of 1): `scenario exceeded its 150 s wall-clock budget after 131864 ticks` (#13, the budget) and `all five: the crew should fell a tree` with the logging crew gone and no tree felled (#10).
- `dlc-ideology-all-four-incidents-run` (0 of 2): `Ideology: the crew should fell a tree` once (#10) and `scenario exceeded its 90 s wall-clock budget after 84710 ticks` once (#13).
- `dlc-royalty-all-four-incidents-run` (1 of 2 passed): `Royalty: the crew should fell a tree` with one crew member left holding CutPlant while its Plants skill was disabled (`speed=0.1 off=True skillOff=True`): #10.
- `work-settings-incident-pawns-have-initialized-work-settings` (0 of 2): `the vanilla raiders whose work settings are initialized ...: expected <> but was <Mila>` (and a list of five names on the first run): #11, a flaw in the scenario's precondition. The contrast was made deterministic afterward and re-run on 1.7.5014 only (5 of 5; see `docs/verification/1.7`); it was not re-run on 1.6.

## Known limits

- RimWorld 1.6.4871 only in this folder; the 1.7 results are in `docs/verification/1.7` (1.7.5014). Windowed 1.6 launches use X11; 1.7 needs `--game-env WAYLAND_DISPLAY=wayland-0` until the runner passes it. The runner has no list of expected vanilla errors, so any vanilla error would fail a run; these are the shared scenario runner's known limits (an expected-error list is missing, and windowed launches on Wayland need the display variable passed). No scenario here uses the 1.7-only click helper.
- Filed on the project's issue tracker (the numbers are the development tracker's and each is described here), none fixed: #9 (a poacher left on the map with no lord and no duty, seen on 1.7 in `dlc-ideology-all-four-incidents-run`, 1 of 7); #10 (a logging crew member with Plants disabled given tree work, or the crew leaving with no tree felled: 1.7 Ideology 1 of 7 and Royalty 1 of 6, and the 1.6 failures above); #11 (the work-settings contrast precondition, a scenario flaw: 1.7 1 of 3, 1.6 2 of 2).
- Also filed, none fixed: #12 (`together-with-factional-war-...` fails 2 of 2 on 1.6 because both assemblies carry the compiler-emitted `Microsoft.CodeAnalysis.EmbeddedAttribute` and `System.Runtime.CompilerServices.RefSafetyRulesAttribute`, which the no-shared-type check reports; passes on 1.7) and #13 (the wall-clock budgets of the DLC scenarios, measured on 1.7, are too tight on 1.6, which runs about 3.5 times slower here). The 1.6 failures in the results table are these and #10 and #11; they are failures of scenarios, not shown to be failures of the mod on 1.6.
- Plants skill: the logging screenshot scenario, like `logging-hostile-crew-fells-trees-and-leaves` and the logging step of the DLC scenarios, sets the Plants skill of the crew pawns that can use it to 12 so that the crew fells a tree. That differs from random raiders, whose crews sometimes fell none (#10).
- 1.6 is slower than 1.7 in these launches (launch 0: about 130 s against about 37 s), so the scenario wall-clock budgets (90 s for a DLC launch, 150 s for all five) are tight on 1.6.
- Vanilla log errors: none were seen on 1.6 in this pass. The vanilla backstory error `No shuffled Adulthood found for hunter of TribeSavage. Choosing random.` (1.7 run, `Verse` and `RimWorld` frames only, no `SR.ModRimWorld.RaidExtension` frame, read from the log by hand because the combination scenario does not check frames; the same family as the trader error in about 1 of 400 generations) is a vanilla bug, not a mod defect; details are in `docs/verification/1.7/README.md`.
- Screenshots: the letter stack sits at the far right and is small at this resolution; some views are dark because the scenario map is at night.
- The caravan half of the Odyssey fix is not observed as an arrival on either version.
- CI runs no scenario. The `identity` job's `apt-get` and tag-fetch steps have not run on the CI runner yet; the first PR run will show.
