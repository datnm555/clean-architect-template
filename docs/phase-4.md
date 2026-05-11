# Phase 4 — Application

**Goal:** Use-case layer (commands + queries) plus the ports they need from the outer world. No framework dependencies except EF Core for `DbSet<T>`.

## Project Created

```
src/Application/
├── Application.csproj                                  refs Domain + SharedKernel, EF Core via CPM
├── Abstractions/
│   ├── Messaging/
│   │   ├── ICommand.cs                                 ICommand + ICommand<TResponse>
│   │   ├── IQuery.cs                                   IQuery<TResponse>
│   │   ├── ICommandHandler.cs                          handler shapes (void + value)
│   │   └── IQueryHandler.cs                            handler shape
│   ├── Data/
│   │   └── IApplicationDbContext.cs                    persistence port — DbSet<Example> + Save
│   └── Authentication/
│       └── IUserContext.cs                             current caller identity
└── Examples/
    ├── CreateExampleCommand.cs                         record with DataAnnotations
    ├── CreateExampleCommandHandler.cs                  internal sealed, uses DbContext
    ├── GetExampleQuery.cs                              IQuery<ExampleResponse>
    ├── GetExampleQueryHandler.cs                       internal sealed, uses repository
    └── Data/
        ├── ExampleResponse.cs                          DTO (never expose entity)
        └── IExampleRepository.cs                       read-side port
```

## What Each File Does

### Messaging abstractions

```csharp
public interface ICommand;
public interface ICommand<TResponse>;
public interface IQuery<TResponse>;

public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    Task<Result> Handle(TCommand command, CancellationToken cancellationToken);
}
public interface ICommandHandler<in TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken);
}
public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken);
}
```
- **Markers, not framework.** No MediatR. Pure interfaces.
- **Type tells you read vs write.** `ICommand` mutates; `IQuery` doesn't. The type system separates the two halves of CQRS.
- **Return type is always `Result` or `Result<T>`.** Failure is in the signature.

### `IApplicationDbContext` — the persistence port
```csharp
public interface IApplicationDbContext
{
    DbSet<Example> Examples { get; }
    DbSet<T> Set<T>() where T : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```
- **Application defines the port, Infrastructure implements it.** Classic Dependency Inversion.
- **Yes, it references EF Core's `DbSet<T>`.** This is the pragmatic Clean Architecture trade-off used by every major .NET template (kid-learning-be, Ardalis, Jason Taylor). A pure version would hide EF entirely behind `IRepository<T>` — clean on paper, painful in practice (no projections, no `Include`, no LINQ).
- **Typed `DbSet<Example>` property** lets handlers write `dbContext.Examples.Add(...)` instead of `dbContext.Set<Example>().Add(...)`.
- **`SaveChangesAsync` is the unit-of-work commit.** Handlers, not repositories, decide when to commit.

### `IUserContext`
```csharp
public interface IUserContext
{
    Guid? UserId { get; }
}
```
- **Nullable** because not every operation has a user (cron jobs, seeds).
- **Implemented in Infrastructure or Web.Api**, reading from `HttpContext`/`ClaimsPrincipal`. Handlers stay HTTP-free.

### `CreateExampleCommand` (write path)
```csharp
public sealed record CreateExampleCommand : ICommand<Guid>
{
    [Required, MinLength(1), MaxLength(200)]
    public string Name { get; init; } = string.Empty;
}
```
- `[Required]` etc. are **documentation today, validation gate later** (a `ValidationDecorator` will read them in Phase 8). The domain's `Example.Create` is still the actual gate.

### `CreateExampleCommandHandler`
```csharp
internal sealed class CreateExampleCommandHandler(IApplicationDbContext dbContext)
    : ICommandHandler<CreateExampleCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateExampleCommand command, CancellationToken ct)
    {
        Result<Example> result = Example.Create(command.Name);
        if (result.IsFailure) return Result.Failure<Guid>(result.Error);

        dbContext.Examples.Add(result.Value);
        await dbContext.SaveChangesAsync(ct);

        return result.Value.Id;
    }
}
```
- **`internal sealed`** — Web.Api never news this up directly; the DI container does. `internal` prevents accidental cross-assembly references.
- **Primary constructor** (`(IApplicationDbContext dbContext)`) — C# 12+ idiom, no boilerplate field.
- **Domain owns validation** — handler just calls `Example.Create` and propagates the `Result`.
- **Direct `dbContext` for writes** — no repository wrapper. Repositories would just delegate to `DbSet<T>`.

### `ExampleResponse` (read DTO) + `IExampleRepository`
```csharp
public sealed record ExampleResponse(Guid Id, string Name);
public interface IExampleRepository
{
    Task<ExampleResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
```
- **Reads return DTOs, never entities.** The most important CQRS rule. Entities are write-side; the read side has its own shape.
- **Returning `?` (nullable)** instead of `Result<T>` for "not found" — keeps the repo signature simple. The handler turns null into `Result.Failure(ExampleErrors.NotFound)`.

### `GetExampleQuery` + handler
```csharp
public sealed record GetExampleQuery(Guid Id) : IQuery<ExampleResponse>;

internal sealed class GetExampleQueryHandler(IExampleRepository repository)
    : IQueryHandler<GetExampleQuery, ExampleResponse>
{
    public async Task<Result<ExampleResponse>> Handle(GetExampleQuery query, CancellationToken ct)
    {
        ExampleResponse? response = await repository.GetByIdAsync(query.Id, ct);
        return response is null
            ? Result.Failure<ExampleResponse>(ExampleErrors.NotFound)
            : response;
    }
}
```

## Architecture Tests Extended (9/9 passing)

`BaseTest.cs` gained:
```csharp
protected static readonly Assembly ApplicationAssembly = typeof(ICommand).Assembly;
```

`Layers/LayerTests.cs` gained three Facts:
- `Application_Should_NotDependOn_Infrastructure`
- `Application_Should_NotDependOn_WebApi`
- `Application_Should_NotDependOn_WebFrameworks` (AspNetCore, Hosting — but **not** EF Core)

A separate `WebFrameworkNamespaces` list now exists alongside `FrameworkNamespaces` because Application is *allowed* to use EF Core but *not* allowed to know about HTTP.

## Major Decision — Pluralize Feature Folders

`Domain.Example` (namespace) conflicted with `Example` (type) during compilation of `IApplicationDbContext.cs`. C#'s namespace resolution walks parent namespaces *before* checking using directives, so `Example` inside `Application.Abstractions.Data` resolved to the `Application.Example` namespace instead of the imported `Domain.Example.Example` type.

Fix: rename feature folders to plural — `Examples/`. The aggregate inside stays singular — `Example`. This matches kid-learning-be's actual practice (`Domain.AuditLogs.AuditLog`).

**Rule going forward:**
- **Feature folders are plural:** `Domain/Examples/`, `Domain/Orders/`, `Application/Examples/`
- **Aggregates inside are singular:** `Example.cs`, `Order.cs`

Renamed:
- `Domain/Example/` → `Domain/Examples/` (3 files)
- `Application/Example/` → `Application/Examples/` (5 files)

Plan and Phase 3 doc updated to match.

## Trade-offs Logged

| Where | We chose | Trade-off |
|---|---|---|
| `ICommand`/`IQuery` marker interfaces | Distinguish read vs write at the type level | Slightly more types vs one `IRequest` |
| `IApplicationDbContext` exposes `DbSet<T>` | Pragmatic — full EF power in handlers | Application now references `Microsoft.EntityFrameworkCore` |
| Repository for reads | Lets caching, projections, joins live in Infrastructure | One more interface per feature |
| Direct `dbContext` for writes | Aggregates and `Add/Remove` flow naturally | Skipped a write-repository abstraction |
| `internal sealed` handlers | Hidden from outer assemblies; DI-only | DI registration must scan internals (Phase 5) |
| Primary constructor on handlers | Less ceremony | Requires C# 12+ |
| Reads return DTOs | Entity stays internal to write side | Two shapes per feature |
| `[Required]`, `[MaxLength]` on commands | Documentation now, validation gate later | Need decorator in Phase 8 to enforce automatically |
| Pluralize folders | Sidesteps namespace/type clash forever | Mass-rename when first detected |

## Build Surprises

| Rule | Why silenced |
|---|---|
| CA1812 | Handlers are `internal sealed`, constructed by DI at runtime — analyzer can't see DI registrations |
| CA2007 | `ConfigureAwait(false)` is noise in modern .NET (no SynchronizationContext) |

Ten total analyzer suppressions now — each with a written `# Reason:` comment.

## Lessons That Generalize

1. **CQRS at the type level.** `ICommand`/`IQuery` markers prevent a query from ever doing a write. Costs nothing; reads better.
2. **Application *owns* the ports, Infrastructure implements them.** `IApplicationDbContext`, `IUserContext`, `IExampleRepository` all live here. Without that ownership, you've just inverted nothing.
3. **Reads return DTOs. Always.** Domain entities have invariants meant for write paths. Surfacing them to read endpoints is how you leak business logic into JSON shapes.
4. **Handlers are 'internal sealed' and resolved via DI.** The outside world doesn't reference them by type — only `ICommand`/`IQuery`.
5. **Pragmatic over pure.** Application referencing EF Core's `DbSet<T>` is a known trade-off — full LINQ ergonomics in exchange for one dependency arrow that almost no one regrets.
6. **Pluralize feature folders, singularize types.** `Domain/Orders/Order.cs`, not `Domain/Order/Order.cs`. The C# compiler will thank you.

## Verification

```
$ dotnet build CleanArchitect.slnx
Build succeeded.   0 Warning(s)   0 Error(s)

$ dotnet test tests/ArchitectureTests/
Passed!  -  Failed: 0, Passed: 9, Skipped: 0, Total: 9, Duration: 35 ms
```

## What's Next

Phase 5 — Infrastructure. We'll create `ApplicationDbContext : DbContext, IApplicationDbContext`, wire EF Core with Postgres, write the first entity configuration, implement `IExampleRepository` over EF, add `DateTimeProvider`, and produce the first migration. The arch tests will gain `Infrastructure_Should_NotDependOn_WebApi`.
