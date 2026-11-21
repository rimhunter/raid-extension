# Contributing

The mod builds from this repository with the .NET SDK: `dotnet build Source/ModRimWorldRaidExtension.csproj -p:GameVersion=1.7` produces the 1.7 load folder, and `-p:GameVersion=1.6` the 1.6 one. `Build.proj` builds both and also needs the shared test runner. `dotnet test --project Tests/RaidExtension.Tests` runs the unit tests. The in-game scenarios in `TestRunner/` need the shared test runner, a private dependency; the maintainers run them on every change. The scenario that runs this mod together with Factional War (rimhunter) loads a build of Factional War's `origin/main` from `external/factional-war` (not committed): set `RIMHUNTER_FACTIONALWAR_SRC` to a Factional War checkout and `scripts/run-all` runs `scripts/prepare-factional-war` first; by hand, run that script, then `scripts/run-scenarios -s <name> --mod external/factional-war`.

Each change is listed with its date in `CHANGELOG.md`, as the Apache License 2.0 asks for a modified version. A pull request adds its own line there.

By contributing you agree that your change is licensed under the same terms as the rest of the mod: the Apache License 2.0 (see `LICENSE`, `NOTICE` and `MODIFICATIONS`).
