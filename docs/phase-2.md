# Phase 2 — Architecture Tests

**Goal:** Lock the Dependency Rule in CI **before** any code can violate it. The architecture test project is the compiler for "dependencies point inward only."

## Project Created

```
tests/ArchitectureTests/
├── ArchitectureTests.csproj   xunit test SDK + NetArchTest.Rules + Shouldly (via CPM)
├── GlobalUsings.cs            global using Xunit;
├── BaseTest.cs                Assembly references per layer (grows as layers are born)
└── Layers/
    └── LayerTests.cs          Dependency Rule assertions
```

## What Each File Does

### `ArchitectureTests.csproj`
Slim test project. Versions come from `Directory.Packages.props` (CPM), so the csproj only names packages — no versions. `IsTestProject=true` lets `dotnet test` discover it.

The `xunit.runner.visualstudio` and `coverlet.collector` references use `PrivateAssets=all` + restricted `IncludeAssets` — standard dance to keep those packages dev-only (not leaked to consumers).

### `Directory.Packages.props` (updated)
Added the six versions Phase 2 needs:
- `Microsoft.NET.Test.Sdk 18.4.0` — `dotnet test` host
- `xunit 2.9.3` + `xunit.runner.visualstudio 3.1.5` — test framework + runner
- `NetArchTest.Rules 1.3.2` — fluent assertions over assembly metadata
- `Shouldly 4.3.0` — assertion library (better failure messages than xUnit's `Assert`)
- `coverlet.collector 10.0.0` — coverage data collector

All versions match what kid-learning-be uses with .NET 10 — known-good.

### `GlobalUsings.cs`
```csharp
global using Xunit;
```
One line. Every test file gets `Xunit` imported automatically — no `using Xunit;` at the top of each test class.

### `BaseTest.cs`
```csharp
protected static readonly Assembly SharedKernelAssembly = typeof(Entity).Assembly;
```
The pattern: one `Assembly` constant per layer, named for the layer. Tests inherit from `BaseTest` to access them. As we add Domain/Application/Infrastructure/Web.Api in later phases, each new layer adds one line here.

Why `typeof(Entity).Assembly` and not `Assembly.Load("SharedKernel")`? **Compile-time safety.** If someone renames the SharedKernel assembly, this won't silently get null; it won't compile.

### `Layers/LayerTests.cs`
Two assertions that pass today:

1. **`SharedKernel_Should_NotDependOn_AnyOuterLayer`** — SharedKernel must not depend on Domain, Application, Infrastructure, or Web.Api. Uses string namespaces, so it works *even before those projects exist*.
2. **`SharedKernel_Should_NotDependOn_Frameworks`** — SharedKernel must not depend on EF Core, ASP.NET, hosting, logging. The kernel must stay framework-free.

A helper `BuildFailureMessage` lists the offending types in the failure message — so when an assertion fails, you instantly see *what* leaked.

As Phases 3–6 add layers, we'll come back here and add assertions:
- `Domain_Should_NotDependOn(Application | Infrastructure | Web.Api)`
- `Application_Should_NotDependOn(Infrastructure | Web.Api)`
- `Infrastructure_Should_NotDependOn(Web.Api)`

## Trade-offs Logged

| Where | We chose | Trade-off |
|---|---|---|
| Test naming `Method_Should_Behavior` | xUnit convention (kid-learning-be too) | Conflicts with CA1707 — silenced for tests |
| Public test classes | xUnit requirement | Conflicts with CA1515 — silenced |
| NetArchTest.Rules over ArchUnitNET | Same as kid-learning-be; fluent + .NET-native | ArchUnitNET supports richer assertions but heavier API |
| Use `NotHaveDependencyOnAny` with string namespaces | Works even when target projects don't exist | Typos go silent; mitigate by centralizing the namespace list |
| Bundle outer-layer + framework checks per layer | One rule = one method, scales linearly | More test methods than strictly needed (vs collapsing into one test) |

## Build Surprises

Two analyzer rules conflict with xUnit conventions:

| Rule | Why we silenced |
|---|---|
| CA1515 | xUnit discovers `public` classes; test classes can't be `internal` |
| CA1707 | `Method_Should_Behavior` underscores are the readable test-name standard |

Both suppressions added to `.editorconfig` with `# Reason:` comments. This is the **fourth time** analyzer suppression has come up — pattern is clear: every framework convention will fight `AnalysisMode=All`, and the fix is targeted suppression with a written reason, not lowering the bar.

## Lessons That Generalize

1. **Architecture tests are guardrails, not paperwork.** Set them up *before* you can violate them. If you wait until Domain exists to write "Domain doesn't depend on Application", it's already too late if someone slipped in a reference.
2. **String-based namespace checks scale.** `NotHaveDependencyOnAny("Domain","Application",...)` works even when those projects haven't been created yet.
3. **`Assembly` references go through a known type (`typeof(Entity).Assembly`)**, not string lookups. Compile-time safety > runtime convention.
4. **One assertion = one Fact.** Don't bundle "doesn't depend on Application AND Infrastructure AND Web.Api" into one test — failure messages get muddy. Tests are cheap; clarity is not.
5. **Failure messages must name the offender.** A green/red dot is not enough. `BuildFailureMessage` lists the failing types so you can fix it without rerunning a debugger.
6. **Every framework convention will conflict with strict analyzers.** Suppress with intent + comment; never disable analyzers globally.

## Verification

```
$ dotnet build CleanArchitect.slnx
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet test tests/ArchitectureTests/
Passed!  -  Failed: 0, Passed: 2, Skipped: 0, Total: 2, Duration: 36 ms
```

## What's Next

Phase 3 — Domain. Once Domain exists, we'll come back to `LayerTests.cs` and add:
- `Domain_Should_NotDependOn_Application`
- `Domain_Should_NotDependOn_Infrastructure`
- `Domain_Should_NotDependOn_Presentation`

The pattern: every time a new layer is born, one new assertion goes here.
