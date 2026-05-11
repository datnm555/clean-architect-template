# Phase 5 — Infrastructure

**Goal:** The outer adapter layer — EF Core, Postgres, DI wiring. Implements the ports Application declared.

## Project Created

```
src/Infrastructure/
├── Infrastructure.csproj                            refs Application, Domain, SharedKernel + EF Core + Npgsql
├── DependencyInjection.cs                           single entry point: AddInfrastructure(IConfiguration)
├── Time/
│   └── DateTimeProvider.cs                          implements IDateTimeProvider
├── Database/
│   ├── ApplicationDbContext.cs                      DbContext + IApplicationDbContext
│   └── Interceptors/
│       └── AuditingInterceptor.cs                   stamps CreatedAt/ModifiedAt on save
└── Examples/
    ├── ExampleConfiguration.cs                      IEntityTypeConfiguration<Example>
    └── ExampleRepository.cs                         implements IExampleRepository
```

## What Each File Does

### `DateTimeProvider.cs` — the simplest adapter
```csharp
internal sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
```
Six lines. Proves the DI wiring works end-to-end. Tests can substitute a fake clock; production gets the real one.

### `ApplicationDbContext.cs` — the database adapter
```csharp
internal sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Example> Examples => Set<Example>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```
- **`internal sealed`** — never referenced outside Infrastructure. Application uses `IApplicationDbContext`.
- **Primary constructor** delegates to `DbContext(options)`.
- **`Examples => Set<Example>()`** — typed property satisfies the interface.
- **`ApplyConfigurationsFromAssembly(...)`** — auto-discovers every `IEntityTypeConfiguration<T>` in this assembly. Add a new entity? Just add its `XxxConfiguration.cs` in `Infrastructure/XxxFeature/` — no `OnModelCreating` edits.

### `AuditingInterceptor.cs` — cross-cutting via interceptor, not domain code
```csharp
internal sealed class AuditingInterceptor(IDateTimeProvider clock) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(...)
    {
        DateTime now = clock.UtcNow;
        foreach (var entry in context.ChangeTracker.Entries<AuditedEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(AuditedEntity.CreatedAt)).CurrentValue = now;
                entry.Property(nameof(AuditedEntity.ModifiedAt)).CurrentValue = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(AuditedEntity.ModifiedAt)).CurrentValue = now;
            }
        }
        ...
    }
}
```
**The key trick:** `entry.Property(name).CurrentValue = value` sets the field via EF's model metadata, bypassing the C# `internal set` accessor. This means `AuditedEntity.CreatedAt` stays `internal set` (untouchable from any business code) but the interceptor — *the only code that's allowed* to stamp timestamps — works fine.

Why this matters: timestamps are **not domain state**. No business logic should ever decide what `ModifiedAt` is. Putting the setter behind the interceptor + an opaque accessor enforces that rule mechanically.

### `ExampleConfiguration.cs` — Fluent API mapping
```csharp
internal sealed class ExampleConfiguration : IEntityTypeConfiguration<Example>
{
    public void Configure(EntityTypeBuilder<Example> builder)
    {
        builder.ToTable("examples");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name)
            .HasMaxLength(SystemConstants.NameMaxLength)
            .IsRequired();
        builder.Ignore(e => e.DomainEvents);
    }
}
```
- **Per-entity file** in the feature folder — same vertical slice pattern as Domain/Application.
- **`builder.Ignore(e => e.DomainEvents)`** — the entity carries events at runtime; they're never persisted. EF would otherwise try to map the collection.
- **Constants come from Domain** (`SystemConstants.NameMaxLength`) — same value as the validator, single source of truth.
- **No annotations on the entity** — domain stays attribute-free; mapping lives in Infrastructure.

### `ExampleRepository.cs` — read-side projection
```csharp
internal sealed class ExampleRepository(IApplicationDbContext dbContext) : IExampleRepository
{
    public Task<ExampleResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Examples
            .Where(e => e.Id == id)
            .Select(e => new ExampleResponse(e.Id, e.Name))
            .FirstOrDefaultAsync(cancellationToken);
}
```
- **Returns DTO, not entity.** The `.Select(...)` projection translates to SQL — only the needed columns are read.
- **No `Include`, no tracking, no domain logic** — pure read path.
- This is the foundation for adding caching, denormalized read models, or read replicas later: change the implementation, the rest of the system doesn't notice.

### `DependencyInjection.cs` — the composition root for Infrastructure
```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
{
    services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
    services.AddSingleton<AuditingInterceptor>();

    string connectionString = configuration.GetConnectionString("Database")
        ?? throw new InvalidOperationException("Connection string 'Database' is missing.");

    services.AddDbContext<ApplicationDbContext>((sp, options) =>
    {
        options.UseNpgsql(connectionString);
        options.AddInterceptors(sp.GetRequiredService<AuditingInterceptor>());
    });

    services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
    services.AddScoped<IExampleRepository, ExampleRepository>();

    return services;
}
```
- **One public method on the whole project.** All `internal sealed` types get wired here.
- **Throw on missing config**, don't fall back to a default — silent fallbacks become production outages.
- **`IApplicationDbContext` resolves to the same instance as `ApplicationDbContext`** in a scope. Command handlers ask for the abstraction, EF asks for the concrete; both get the same object.
- **Interceptors register as `AddSingleton`** but bind into options via `sp.GetRequiredService`. That's how a singleton interceptor gets the singleton `IDateTimeProvider`.

## Architecture Tests Extended (11/11 passing)

`BaseTest.cs` gained:
```csharp
protected static readonly Assembly InfrastructureAssembly = typeof(DependencyInjection).Assembly;
```

`Layers/LayerTests.cs` gained two `[Fact]`s:
- `Infrastructure_Should_NotDependOn_WebApi` ✅
- `Infrastructure_Should_NotDependOn_WebFrameworks` ✅ (no `Microsoft.AspNetCore`, no `Microsoft.Extensions.Hosting`)

Infrastructure may use EF Core, Npgsql, configuration, DI — but **must not** know about HTTP or hosting. That's Web.Api's job.

## Deferred — 5.7 First Migration

`dotnet ef migrations add Initial` requires either a startup project or a design-time factory. **Plan: run it in Phase 6** once Web.Api becomes the natural startup project (kid-learning-be does the same).

Migration command for Phase 6 (per `kid-learning-be`'s CLAUDE.md pattern):
```bash
dotnet ef migrations add Initial \
  --project src/Infrastructure \
  --startup-project src/Web.Api \
  --output-dir Database/Migrations \
  --context ApplicationDbContext \
  -- --environment Migration
```

Notes for that step:
- `--output-dir Database/Migrations` is required — otherwise EF creates `Infrastructure/Migrations/` and the model gets confused.
- After generation, change `public partial` → `internal partial` on both the migration class and its `.Designer.cs` (Infrastructure types are `internal`).
- We'll also need to add `Microsoft.EntityFrameworkCore.Design` to Infrastructure and `Microsoft.EntityFrameworkCore.Tools` to Web.Api in Phase 6.

## Trade-offs Logged

| Where | We chose | Trade-off |
|---|---|---|
| Pragmatic Clean Architecture (Application sees `DbSet<T>`) | Full LINQ in handlers; less ceremony | Application references EF Core |
| EF `PropertyEntry` to set internal audit fields | Encapsulation preserved; no `InternalsVisibleTo` needed | Subtler than `entry.Entity.CreatedAt = now` would be |
| `internal sealed` everywhere in Infrastructure | Hidden from outer assemblies; DI-only | DI registration is the only seam |
| Interceptor for audit, not domain logic | Timestamps are not business data | Domain entities can't see "when was I last touched" without a query |
| Repository returns DTOs via `.Select(...)` projection | SQL fetches only needed columns | Two shapes per read — entity + DTO |
| Single `DependencyInjection.cs` per layer | Easy to find wiring | Will grow; split per concern when it does |
| Postgres (Npgsql) baked into Infrastructure | Concrete choice; less abstraction | Swap DB = new Npgsql call sites to find |
| Postpone migration to Phase 6 | Avoid design-time factory boilerplate | Domain model exists but no SQL artifact yet |

## Build Surprises — none this round
Zero new analyzer suppressions. Ten total carried over; the existing suppressions cover every pattern we used here.

## Lessons That Generalize

1. **Infrastructure implements ports it doesn't own.** `IDateTimeProvider`, `IApplicationDbContext`, `IExampleRepository` — all defined in inward layers. Infrastructure is the *plug*, not the *outlet*.
2. **One `DependencyInjection.cs` per layer.** Web.Api will call `AddInfrastructure(builder.Configuration)` — never reference an internal type directly.
3. **EF interceptors are the right hook for cross-cutting state.** Audit timestamps, soft-delete flags, outbox writes — all belong in interceptors, never in handlers or entities.
4. **Read with projections (`.Select`), not `.Include`.** Projections generate narrow SQL; Includes cartesian-explode and pull entire entities. Reads belong on the projection path.
5. **Fluent EF config in feature folders.** `Infrastructure/Examples/ExampleConfiguration.cs` mirrors `Domain/Examples/Example.cs`. The vertical slice is consistent across layers.
6. **Throw on missing config.** Silent fallbacks become production outages.
7. **EF's `PropertyEntry` bypasses C# access modifiers via model metadata.** Use this when you want to *enforce* that only the persistence layer can mutate a field — `internal set` plus an interceptor is a powerful pattern.

## Verification

```
$ dotnet build CleanArchitect.slnx
Build succeeded.   0 Warning(s)   0 Error(s)

$ dotnet test tests/ArchitectureTests/
Passed!  -  Failed: 0, Passed: 11, Skipped: 0, Total: 11, Duration: 49 ms
```

## What's Next

Phase 6 — Web.Api. We'll create the host project, wire `AddInfrastructure` + a new `AddApplication` (to register handlers), add the `IEndpoint` pattern with auto-discovery, build `CreateExample.cs` and `GetExample.cs` endpoints, map `Result<T>` → HTTP, and **finally generate the first migration**. The arch tests have no new assertions to add at that point — Web.Api may depend on everything; the rule chain is complete.
