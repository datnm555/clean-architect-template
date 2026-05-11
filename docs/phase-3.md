# Phase 3 — Domain

**Goal:** Pure business code. Zero outward dependencies. The most stable layer in the system.

## Project Created

```
src/Domain/
├── Domain.csproj                            references SharedKernel
├── SystemConstants.cs                       cross-feature constants
└── Examples/
    ├── Example.cs                           the aggregate root
    ├── ExampleErrors.cs                     static factories for domain errors
    └── ExampleCreatedDomainEvent.cs         record : IDomainEvent
```

## What Each File Does

### `SystemConstants.cs`
```csharp
public static class SystemConstants
{
    public const int NameMinLength = 1;
    public const int NameMaxLength = 200;
}
```
Cross-feature constants — values that domain validators reuse. Lives in the Domain root namespace, not in any feature folder, because multiple features may share the rules.

### `Examples/Example.cs` (the aggregate)
- `sealed class Example : AggregateRoot` — sealed because aggregates aren't extended.
- **Private parameterless constructor** — EF Core uses it (via reflection) for materialization. No domain code calls `new Example()` directly.
- **`Name` has `private set`** — outside code can't mutate name. State changes go through methods.
- **`static Result<Example> Create(string name)`** — the only way to construct an `Example` from application code. Validates, then either returns `Result.Failure<Example>(error)` or a fully-initialized aggregate.
- **`Result Rename(string name)`** — a behavior method demonstrating that state changes also return `Result`. Same validation centralized via `ExampleErrors`.
- **`Guid.CreateVersion7()`** — sortable GUIDs (.NET 9+). Better than v4 for DB index locality.
- **`example.Raise(new ExampleCreatedDomainEvent(...))`** — domain events live on the aggregate; the dispatcher picks them up after `SaveChangesAsync` (Phase 5).

### `Examples/ExampleErrors.cs`
```csharp
public static readonly Error NotFound = Error.NotFound("Example.NotFound", "...");
public static readonly Error NameRequired = Error.Validation("Example.NameRequired", "...");
public static Error NameTooLong(int actualLength) => Error.Validation(
    "Example.NameTooLong",
    $"...max {SystemConstants.NameMaxLength} chars (got {actualLength}).");
```
- Pattern: **`{Entity}.{ErrorName}`** for the `Code` field (`"Example.NotFound"`). Greppable, mappable to HTTP status, stable for clients.
- Static readonly fields for parameterless errors; static methods for parameterized errors (need extra context like `actualLength`).
- Lives next to the entity — DDD vertical slice. Errors are part of the entity's vocabulary.

### `Examples/ExampleCreatedDomainEvent.cs`
```csharp
public sealed record ExampleCreatedDomainEvent(Guid ExampleId, string Name) : IDomainEvent;
```
One line. A record implementing the marker `IDomainEvent`. Carries just enough payload to identify the aggregate and any data subscribers need to react. **Never includes the entity itself** — events go across boundaries (could be serialized to an outbox); entities don't serialize cleanly.

## How the Pattern Fits Together

```
1. Application calls: Example.Create("widget")
2. Example.Create validates name        ──► returns Result.Failure on bad input
3. On success, constructs Example, raises ExampleCreatedDomainEvent
4. Returns Result<Example> wrapping the new aggregate
5. Application persists via DbContext, then dispatches DomainEvents
```

The whole flow is **failure-as-data, not failure-as-exception**. The handler that calls `Create` knows from the type system there could be a failure and must check it. No invisible control flow.

## Architecture Tests Extended

`BaseTest.cs` gained:
```csharp
protected static readonly Assembly DomainAssembly = typeof(Example).Assembly;
```

`Layers/LayerTests.cs` gained four assertions:
- `Domain_Should_NotDependOn_Application`
- `Domain_Should_NotDependOn_Infrastructure`
- `Domain_Should_NotDependOn_WebApi`
- `Domain_Should_NotDependOn_Frameworks`

All passing. **6/6 architecture tests green.** The Domain layer is now compiler-enforced as inward-only.

## Trade-offs Logged

| Where | We chose | Trade-off |
|---|---|---|
| `sealed class Example` | Aggregates are leaves, not extension points | Loses subclassing — usually a feature, not a bug |
| Private parameterless ctor + static `Create` | Forces validated construction; EF uses it via reflection | One line of boilerplate per aggregate |
| `Result<Example>` from `Create` | Failure is part of the type | More verbose than throwing |
| `Guid.CreateVersion7()` for Id | Sortable, better B-tree locality | .NET 9+ only; v4 if older runtimes matter |
| `Name` with `private set` | State changes go through methods | EF needs reflection to set on load — works fine |
| Errors as `public static` factories | Greppable, reusable, testable | Slightly more code than throwing ad-hoc errors |
| Domain event carries minimal data (`Id`, `Name`) | Event survives serialization across an outbox | Subscriber needing more data must re-query |

## Build Surprise — CA1724

`Example` (the class) lives inside `Domain.Examples` (the namespace). CA1724 fires on the matching root word. **This is the standard DDD vertical-slice pattern** — feature folder named after the aggregate root. kid-learning-be silences this rule for the same reason.

Suppressed in `.editorconfig` with `# Reason:` comment. Eighth analyzer rule we've muted; the pattern continues: framework conventions vs strict analyzers; we win every fight with a targeted suppression + comment.

## Lessons That Generalize

1. **Entities are the heart of the domain.** Behavior lives on them (`Rename`, `Cancel`, `Approve`), not in service classes. Anemic entities = procedural code in a class shape.
2. **Validation belongs in the entity, not in handlers.** `Example.Create` is the single source of truth for "what is a valid Example." Handlers can't bypass it.
3. **Errors are values, not exceptions.** A failed `Create` returns `Result.Failure<Example>(...)`. Bugs throw; expected outcomes return.
4. **Domain events name what happened, not what to do.** `ExampleCreatedDomainEvent`, not `SendWelcomeEmailCommand`. The handlers decide the consequence.
5. **EF Core constraints inform Domain design.** Private parameterless ctors, mutable Id via `protected internal set` — these are concessions to the persistence story without leaking persistence concepts into Domain.
6. **Vertical slice inside Domain.** Everything for `Example` (entity, errors, events) lives in `Domain/Examples/`. When you add `Order`, you copy the folder shape, not horizontally split into `entities/`, `errors/`, `events/` folders.

## Verification

```
$ dotnet build CleanArchitect.slnx
Build succeeded.    0 Warning(s)  0 Error(s)

$ dotnet test tests/ArchitectureTests/
Passed!  -  Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 49 ms
```

## What's Next

Phase 4 — Application. We'll build the use-case layer:
- Abstractions: `ICommand`/`IQuery` + handlers, `IApplicationDbContext`, `IUserContext`
- One feature end-to-end: `CreateExampleCommand` + handler, `GetExampleQuery` + handler, `IExampleRepository` + `ExampleResponse` DTO
- Extend `LayerTests`: Application → not depend on Infrastructure or Web.Api
