# Build Plan — Clean Architecture .NET Template

A step-by-step build order for assembling a Clean Architecture + DDD + CQRS + Vertical Slice .NET template from scratch. Inspired by `kid-learning-be` but kept thin: only the architectural skeleton, no production patterns (Outbox, audit, locking, etc.) until they're earned.

## Mental Model

```
SharedKernel  →  Domain  →  Application  →  Infrastructure  →  Web.Api
   (no deps)   (kernel)   (domain+kernel)   (app+domain+    (all of them)
                                              kernel)
```

**Build order = dependency order.** A class can never reference something that doesn't exist yet, so build the most-depended-on thing first. Same rule for files inside a layer: "if I create X before Y, does Y compile?"

## The Three Rules

1. **Dependencies point inward only.** `ArchitectureTests` is the compiler for this rule.
2. **Inside a layer, create the most-depended-on file first.**
3. **Build one vertical slice end-to-end before adding more features.** A single feature that compiles, runs, and has tests is worth ten half-built ones.

---

## Phase 0 — Solution Scaffolding

Goal: establish the workspace so every future file lands in the right place.

| Step | Create | Why |
|---|---|---|
| 0.1 | `global.json` pinning .NET SDK | Stops "works on my machine" SDK drift |
| 0.2 | `Directory.Build.props` | Sets `LangVersion`, `Nullable`, `TreatWarningsAsErrors` once for every project |
| 0.3 | `Directory.Packages.props` (Central Package Management) | Versions in one file, not in 7 `.csproj`s |
| 0.4 | `.editorconfig` | Enforces style; analyzers read it |
| 0.5 | Empty solution: `dotnet new sln -n CleanArchitect` | The container |

**Verify:** `dotnet build` succeeds on the empty solution.

---

## Phase 1 — SharedKernel project

Goal: primitives every other layer can use. No business logic.

```bash
dotnet new classlib -o src/SharedKernel
dotnet sln add src/SharedKernel
```

Create in this exact order:

| # | File | Why this order |
|---|---|---|
| 1.1 | `Error.cs` (record: `Code`, `Description`, `Type`) | Everything else returns/holds Errors |
| 1.2 | `ErrorType.cs` (enum: `Failure`, `Validation`, `Problem`, `NotFound`, `Conflict`) | Maps errors → HTTP status |
| 1.3 | `Result.cs` and `Result<T>.cs` | Use cases return Result; depends on Error |
| 1.4 | `ValidationError.cs` (extends Error) | Specialized Error for input validation |
| 1.5 | `IDomainEvent.cs` (marker interface) | Needed by Entity |
| 1.6 | `Entity.cs` (abstract: `Id`, equality by Id) | Base for every domain object |
| 1.7 | `AggregateRoot.cs` (extends Entity, holds `DomainEvents`) | Aggregate boundary for events |
| 1.8 | `ValueObject.cs` (abstract: equality by components) | Used for `Money`, `Email`, etc. |
| 1.9 | `IDateTimeProvider.cs` (abstract clock) | Inject time, never `DateTime.UtcNow` directly |
| 1.10 | `AuditedEntity.cs` (extends Entity: `CreatedAt`, `ModifiedAt`) | Most entities are audited |
| 1.11 | `IDomainEventHandler<T>.cs` | Wiring for domain events |

---

## Phase 2 — Architecture Tests project

Goal: lock the dependency direction in CI before you can violate it.

```bash
dotnet new xunit -o tests/ArchitectureTests
```

Packages: `NetArchTest.Rules`, `Shouldly`.

| # | File | Why |
|---|---|---|
| 2.1 | `BaseTest.cs` with namespace constants | DRY for namespace strings |
| 2.2 | `Layers/LayerTests.cs` — Domain has no outward refs | The single most important test |

Add more assertions as each layer is born. Run `dotnet test tests/ArchitectureTests/` after every layer.

---

## Phase 3 — Domain project

```bash
dotnet new classlib -o src/Domain
dotnet add src/Domain reference src/SharedKernel
```

Build abstractions first, then one example feature.

| # | File | Why this order |
|---|---|---|
| 3.1 | `SystemConstants.cs` (id length, etc.) | Used by validators below |
| 3.2 | `Domain/Example/Example.cs` (private ctor + `Create()` returning `Result<Example>`) | Entity is the anchor |
| 3.3 | `Domain/Example/ExampleErrors.cs` (static factories: `NotFound`, `AlreadyExists`) | Errors live next to the entity |
| 3.4 | `Domain/Example/ExampleCreatedDomainEvent.cs` (record) | Demonstrates the event flow |

**Verify:** arch test — Domain has zero outward refs.

---

## Phase 4 — Application project

```bash
dotnet new classlib -o src/Application
dotnet add src/Application reference src/Domain src/SharedKernel
```

### 4a. Abstractions

| # | File | Why |
|---|---|---|
| 4.1 | `Abstractions/Messaging/ICommand.cs`, `ICommand<TResponse>.cs` | Types tell readers "this is a write" |
| 4.2 | `Abstractions/Messaging/IQuery<TResponse>.cs` | Same for reads |
| 4.3 | `Abstractions/Messaging/ICommandHandler.cs`, `IQueryHandler.cs` | Handler shapes |
| 4.4 | `Abstractions/Data/IApplicationDbContext.cs` (with `DbSet<...>` properties, `SaveChangesAsync`) | Persistence port — implemented by Infrastructure |
| 4.5 | `Abstractions/Authentication/IUserContext.cs` | Don't read from HttpContext inside handlers |

### 4b. One feature end-to-end

| # | File | Why this order |
|---|---|---|
| 4.6 | `Application/Example/CreateExampleCommand.cs` (record with DataAnnotations) | Inputs first |
| 4.7 | `Application/Example/CreateExampleCommandHandler.cs` (internal sealed) | Uses command + DbContext |
| 4.8 | `Application/Example/Data/ExampleResponse.cs` (DTO) | Never expose Entity |
| 4.9 | `Application/Example/Data/IExampleRepository.cs` (read-side interface) | Port for query handler |
| 4.10 | `Application/Example/GetExampleQuery.cs` + handler | Read path |

**Verify:** Application doesn't reference Infrastructure (arch test).

---

## Phase 5 — Infrastructure project

```bash
dotnet new classlib -o src/Infrastructure
dotnet add src/Infrastructure reference src/Application src/Domain src/SharedKernel
```

Packages: `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.Extensions.DependencyInjection.Abstractions`.

| # | File | Why this order |
|---|---|---|
| 5.1 | `Time/DateTimeProvider.cs` (implements `IDateTimeProvider`) | Easy first adapter — proves DI wiring |
| 5.2 | `Database/ApplicationDbContext.cs` (extends `DbContext`, implements `IApplicationDbContext`, `internal`) | The DB adapter |
| 5.3 | `Database/Interceptors/AuditingInterceptor.cs` (sets CreatedAt/ModifiedAt) | Pluggable cross-cutting |
| 5.4 | `Example/ExampleConfiguration.cs` (`IEntityTypeConfiguration<Example>`) | Per-entity EF mapping |
| 5.5 | `Example/ExampleRepository.cs` (implements `IExampleRepository`) | Read adapter |
| 5.6 | `DependencyInjection.cs` — `IServiceCollection.AddInfrastructure(IConfiguration)` | Single entry point for wiring |
| 5.7 | First EF migration | `dotnet ef migrations add Initial …` |

**Verify:** `dotnet ef migrations add Initial` succeeds; arch test still green.

---

## Phase 6 — Web.Api project

```bash
dotnet new web -o src/Web.Api
dotnet add src/Web.Api reference src/Application src/Infrastructure src/Domain src/SharedKernel
```

| # | File | Why this order |
|---|---|---|
| 6.1 | `Infrastructure/IEndpoint.cs` (interface with `MapEndpoint(IEndpointRouteBuilder)`) | Shape every endpoint follows |
| 6.2 | `Infrastructure/EndpointExtensions.cs` (reflection-based auto-registration) | No giant `Program.cs` |
| 6.3 | `Middleware/ResultExtensions.cs` (Result → IResult mapping by `ErrorType`) | Centralize HTTP mapping |
| 6.4 | `Middleware/GlobalExceptionHandler.cs` | Catch-all safety net |
| 6.5 | `Endpoints/Example/CreateExample.cs` (implements IEndpoint) | First end-to-end vertical |
| 6.6 | `Endpoints/Example/GetExample.cs` | Read endpoint |
| 6.7 | `Program.cs` — call `AddInfrastructure`, `AddApplication`, `MapEndpoints` | Bootstrap last |

**Verify:** `dotnet run --project src/Web.Api`, hit `POST /example` and `GET /example/{id}`.

---

## Phase 7 — Test scaffolding

```bash
dotnet new xunit -o tests/Application.UnitTests
dotnet new xunit -o tests/Api.IntegrationTests
```

| # | What | Why |
|---|---|---|
| 7.1 | Unit: `CreateExampleCommandHandlerTests` with NSubstitute + MockQueryable.NSubstitute | Handler isolation — no DB |
| 7.2 | Integration: `WebApplicationFactory<Program>` + Testcontainers Postgres, hit API only | Full stack through HTTP |
| 7.3 | Add to `ArchitectureTests`: handlers must be `internal sealed`, end with `CommandHandler`/`QueryHandler`, return `Task<Result>` | Convention → compile-time rule |

**Verify:** `dotnet test` runs three projects, all green.

---

## Phase 8 — Cross-cutting (only when earned)

Add in this order, only when you hit the pain:

| Add when… | Pattern | Where |
|---|---|---|
| Handlers need validation | `ValidationDecorator` wrapping `ICommandHandler` | Application/Abstractions/Behaviors |
| Need structured logs around every handler | `LoggingDecorator` | Same |
| Publishing events to other systems | **Outbox** pattern | Domain/Outbox + Infrastructure/Outbox |
| Who-did-what for compliance | `AuditLog` entity + interceptor | Domain + Infrastructure |
| Replicas race on the same job | `IDistributedLockProvider` + Redis adapter | Application + Infrastructure |
| Reads kill the DB | `CachedRepository` over `HybridCache` | Application/<Feature>/Data |
| Auth needed | Identity / JWT / `IUserContext` impl | Infrastructure/Authentication |
| Observability needed | OpenTelemetry decorator + exporters | Application + Web.Api |

**Principle:** every cross-cutting piece is an answer to a real problem. Don't pre-install them.
