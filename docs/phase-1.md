# Phase 1 — SharedKernel

**Goal:** Primitives every other layer can use, with zero dependencies of its own. The most-depended-on project; must stay minimal.

## Project Created

```
src/SharedKernel/
├── SharedKernel.csproj    bare SDK ref — Directory.Build.props supplies the rest
├── Error.cs               error handling
├── ErrorType.cs
├── Result.cs              + Result<T>
├── ValidationError.cs
├── Entity.cs              domain primitives
├── AggregateRoot.cs
├── ValueObject.cs
├── AuditedEntity.cs
├── IDomainEvent.cs        cross-cutting interfaces
├── IDomainEventHandler.cs
└── IDateTimeProvider.cs
```

## What Each File Does

### Error handling — the Result pattern

- **`Error.cs`** — record with `Code`, `Description`, `Type`, plus static factories (`Error.NotFound`, `Error.Conflict`, …). Domain errors are *values*, not exceptions.
- **`ErrorType.cs`** — enum: `Failure | Validation | Problem | NotFound | Conflict`. Five categories that map cleanly to HTTP status codes.
- **`Result.cs` / `Result<TValue>`** — non-exception error flow. Use cases return one of these. Class invariant forbids invalid states (success + error, or failure + no error). `Value` getter throws if you read it on a failure, forcing callers to check `IsSuccess` first. Implicit `T → Result<T>` is sugar so handlers can `return user;`.
- **`ValidationError.cs`** — bundles many field errors into one `Error`. `FromResults(IEnumerable<Result>)` collects all failing results — APIs return all field errors at once, not one-at-a-time.

### Domain primitives

- **`Entity.cs`** — abstract base. `Guid Id`, equality by Id (with `Id != Guid.Empty` guard so unsaved entities don't falsely compare equal), domain-event collection with `Raise()` / `ClearDomainEvents()`.
- **`AggregateRoot.cs`** — marker subclass of Entity. Documents which entities are aggregate roots (the transactional boundary; only roots have repositories).
- **`ValueObject.cs`** — equality by *components*. Subclasses override `GetAtomicValues()` to yield each property; base class walks them in lockstep for `Equals`/`GetHashCode`.
- **`AuditedEntity.cs`** — adds `CreatedAt` / `ModifiedAt` with `internal` setters. An EF interceptor will fill these in Phase 5; domain code never touches the clock directly.

### Cross-cutting interfaces

- **`IDomainEvent.cs`** — empty marker interface. Anything implementing it is "a thing that happened in the domain."
- **`IDomainEventHandler<TDomainEvent>`** — `Task Handle(TDomainEvent, CancellationToken)`. Implemented in Application or Infrastructure; a dispatcher invokes after `SaveChangesAsync`.
- **`IDateTimeProvider.cs`** — `DateTime UtcNow { get; }`. The single most important abstraction here. **Never call `DateTime.UtcNow` directly in domain or application code.** Mock it in tests; one place to control time.

## Trade-offs Logged

| Where | We chose | Trade-off |
|---|---|---|
| Result type | Roll our own | Zero deps in SharedKernel beats reusing Ardalis.Result or CSharpFunctionalExtensions. |
| `Entity` | Skipped `IsDeleted`, `Version`, `[JsonInclude]` (vs kid-learning-be) | Soft-delete + optimistic concurrency are policy choices, not universal. Add when earned. |
| `AuditedEntity` | Two fields (`CreatedAt`, `ModifiedAt`) | kid-learning-be has six. Trimmed — `DeletedAt` belongs with soft-delete; `CreatedBy`/`UpdatedBy` need user identity. |
| `ValueObject` | Custom abstract class, not `record` | A shared base type lets libraries do `if (x is ValueObject)`. Records can't share that. |
| `IDateTimeProvider` | Our own interface, not BCL `TimeProvider` | Cleaner call sites; switchable later. 4-line file. |
| `IDomainEvent` | Empty marker interface | More discoverable than an attribute, lets you write `IEnumerable<IDomainEvent>`. CA1040 silenced. |

## Build Surprises (and Lessons)

### `NuGet.config` — repo-scoped package sources
The dev's global NuGet had two sources (`nuget.org` + `github_autumn`); Central Package Management warns; `TreatWarningsAsErrors=true` turned that into a failure. Fix: add a repo-local `NuGet.config` that clears sources and pins to nuget.org only.

> **Lesson:** a template must not depend on the developer's machine config. Anything that ties build success to global state is a bug.

### `.editorconfig` — seven analyzer suppressions, each with a justification
`AnalysisMode=All` fights idiomatic DDD/Result patterns. We silenced:

| Rule | Why |
|---|---|
| CA1000 | `Result<T>.Success/Failure` static factories are idiomatic |
| CA1030 | `Entity.Raise(IDomainEvent)` is DDD events, not C# events |
| CA1040 | `IDomainEvent` is a marker interface — deliberate |
| CA1711 | `IDomainEventHandler` ends in 'EventHandler' deliberately |
| CA1716 | `Error` is the canonical Result-pattern name |
| CA1819 | `ValidationError.Errors` exposes an array on purpose |
| CA2225 | Implicit `T → Result<T>` is ergonomic, no named alternate needed |

> **Lesson:** every analyzer suppression carries a `# Reason:` comment. Silencing without justification is technical debt.

## Lessons That Generalize

1. **Build the most-depended-on file first.** `Error` → `Result` → `ValidationError`, never the other way.
2. **SharedKernel has zero outward dependencies.** No EF, no logging, no JSON, no framework. The moment one creeps in, the whole architecture is poisoned.
3. **Replace exceptions with `Result<T>` for expected outcomes.** Bugs throw; business errors return `Result.Failure(...)`.
4. **Marker interfaces, internal setters, abstract bases** are the load-bearing scaffolding. Small individually, reinforce each other.
5. **`AnalysisMode=All` + `TreatWarningsAsErrors=true` will fight you. Suppress with intent and a comment, never lower the global bar.**

## Verification

```
$ dotnet build CleanArchitect.slnx
SharedKernel -> .../SharedKernel/bin/Debug/net10.0/SharedKernel.dll
Build succeeded.
    0 Warning(s)
    0 Error(s)
```
