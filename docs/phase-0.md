# Phase 0 — Solution Scaffolding

**Goal:** Establish the workspace so every future file lands in the right place.

## Files Created

| File | Purpose |
|---|---|
| `global.json` | Pin SDK to .NET 10, allow latest minor roll-forward |
| `Directory.Build.props` | MSBuild props inherited by every project (TFM, nullable, analyzers, warnings-as-errors) |
| `Directory.Packages.props` | Enables Central Package Management — versions in one file |
| `.editorconfig` | Style + naming rules consumed by IDE and analyzers |
| `CleanArchitect.slnx` | Empty solution container (new XML solution format, .NET 9+) |

## Key Decisions

### Strict on day one
- `AnalysisLevel=latest`, `AnalysisMode=All`, `TreatWarningsAsErrors=true`, `EnforceCodeStyleInBuild=true`.
- Turning these on later means fixing 500 warnings at once. Day-one strictness costs almost nothing.

### Configure once, inherit everywhere
- `Directory.Build.props` removes per-project duplication. After it runs, a `.csproj` can shrink to one line: `<Project Sdk="Microsoft.NET.Sdk" />`.
- Central Package Management means upgrading EF Core is one line, not seven.

### .slnx over .sln
- New XML solution format. Cleaner diffs, supported since .NET 9. `kid-learning-be` uses this; we follow.

### LangVersion=latest
- Lets us use C# 14 features on .NET 10 (`field` keyword, primary constructors, collection expressions).

## Trade-offs Logged

| Choice | Trade-off |
|---|---|
| `TreatWarningsAsErrors=true` | A transitive-dep warning breaks the build. Worth it — most warnings are real. |
| `rollForward: latestMinor` | Accepts 10.x but not 11.x. Predictable upgrades. |
| Bare csproj (`<Project Sdk="Microsoft.NET.Sdk" />`) | `dotnet new` generates a verbose csproj; we slim it manually each time. Tiny ceremony, big reduction in drift. |

## Lessons That Generalize

1. **Configure once, inherit everywhere.** `Directory.Build.props` + CPM keep `.csproj` files trivial.
2. **Pin the SDK.** A 30-second `global.json` saves multi-hour "works on my machine" debugging.
3. **Strict from day one.** Painless to enable empty; painful to retrofit.
4. **Style is policy, not preference.** `.editorconfig` makes "do we use `var`?" a compiler concern.
5. **A template must not depend on the developer's machine.** This rule will bite us in Phase 1.

## Verification

```
$ dotnet build CleanArchitect.slnx
Build succeeded.
    0 Error(s)
```
(Warning about empty solution is expected — no projects added yet.)
