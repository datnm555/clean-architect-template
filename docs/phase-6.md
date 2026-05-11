# Phase 6 — Web.Api

**Goal:** The delivery layer. HTTP host, route bindings, Result→HTTP mapping, exception handler, composition root. The whole template comes to life here.

## Project Created

```
src/Web.Api/
├── Web.Api.csproj                              SDK=Web, refs all 4 projects + EF Design (dev only)
├── Program.cs                                  bootstrap: AddApplication + AddInfrastructure + MapEndpoints
├── appsettings.json                            connection string + logging
├── Infrastructure/
│   ├── IEndpoint.cs                            interface every endpoint implements
│   └── EndpointExtensions.cs                   reflection-based auto-registration + mapping
├── Middleware/
│   ├── ResultExtensions.cs                     Result/Result<T> → IResult by ErrorType
│   └── GlobalExceptionHandler.cs               IExceptionHandler catch-all
└── Endpoints/
    └── Examples/
        ├── CreateExample.cs                    POST /examples — IEndpoint impl
        └── GetExample.cs                       GET /examples/{id} — IEndpoint impl

src/Application/
└── DependencyInjection.cs                      AddApplication — registers internal handlers

src/Infrastructure/
└── Database/Migrations/                        ← 5.7 first migration committed here
    ├── 20260511181635_Initial.cs
    ├── 20260511181635_Initial.Designer.cs
    └── ApplicationDbContextModelSnapshot.cs

.config/
└── dotnet-tools.json                           locks dotnet-ef to project (10.0.7)
```

## What Each File Does

### `Infrastructure/IEndpoint.cs`
```csharp
public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}
```
Single method. Every concrete endpoint implements it and calls `app.MapGet/MapPost/…`. **No base class, no MediatR, no controller**.

### `Infrastructure/EndpointExtensions.cs`
Two pieces:

- **`AddEndpoints(this IServiceCollection, Assembly)`** — uses `assembly.DefinedTypes` to find every non-abstract type implementing `IEndpoint` and registers them as `IEnumerable<IEndpoint>`. Add a new endpoint? Just create the class. Reflection finds it.
- **`MapEndpoints(this IEndpointRouteBuilder)`** — resolves the `IEnumerable<IEndpoint>` and calls `MapEndpoint` on each.

Together they let Program.cs stay ~10 lines no matter how many endpoints exist.

### `Middleware/ResultExtensions.cs`
The bridge between the `Result<T>` pattern and HTTP:
```csharp
public static IResult ToHttpResult<TValue>(
    this Result<TValue> result,
    Func<TValue, IResult>? onSuccess = null) =>
    result.IsSuccess
        ? (onSuccess?.Invoke(result.Value) ?? Results.Ok(result.Value))
        : MapError(result.Error);

private static IResult MapError(Error error) => error.Type switch
{
    ErrorType.NotFound   => Results.NotFound(...),
    ErrorType.Validation => Results.BadRequest(...),
    ErrorType.Conflict   => Results.Conflict(...),
    ErrorType.Problem    => Results.Problem(... 400 ...),
    _                    => Results.Problem(... 500 ...),
};
```
The `onSuccess` factory is optional — pass it when you want a non-default success response (e.g., 201 Created with a Location header). Everything else uses sensible defaults.

### `Middleware/GlobalExceptionHandler.cs`
```csharp
internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(new { title = "Server failure", status = 500 }, ct);
        return true;
    }
}
```
Modern .NET's `IExceptionHandler` interface (not the old middleware pattern). Logs + returns a clean 500 without leaking stack traces. Wired in Program.cs.

### `Endpoints/Examples/CreateExample.cs`
```csharp
internal sealed class CreateExample : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/examples", async (
            CreateExampleCommand command,
            ICommandHandler<CreateExampleCommand, Guid> handler,
            CancellationToken ct) =>
        {
            Result<Guid> result = await handler.Handle(command, ct);
            return result.ToHttpResult(id => Results.Created($"/examples/{id}", new { id }));
        });
    }
}
```
- The endpoint **injects the handler interface**, not the implementation. DI resolves `CreateExampleCommandHandler` for it.
- **No dispatcher / mediator** between endpoint and handler — one fewer indirection.
- Success → 201 Created with `Location` header; failure → mapped via `ToHttpResult`.

### `Endpoints/Examples/GetExample.cs`
```csharp
app.MapGet("/examples/{id:guid}", async (
    Guid id,
    IQueryHandler<GetExampleQuery, ExampleResponse> handler,
    CancellationToken ct) =>
{
    Result<ExampleResponse> result = await handler.Handle(new GetExampleQuery(id), ct);
    return result.ToHttpResult();
});
```
- Route constraint `{id:guid}` rejects malformed IDs at the framework level (404 before reaching the handler).
- Default success mapping → 200 OK with the DTO; not-found → 404 with the error code.

### `Program.cs`
```csharp
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEndpoints(typeof(Program).Assembly);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

WebApplication app = builder.Build();
app.UseExceptionHandler();
app.MapEndpoints();
app.Run();

public partial class Program;
```
- Three composition calls, three pipeline calls. That's the whole bootstrap.
- **`public partial class Program;`** — required so `WebApplicationFactory<Program>` can find it in integration tests (Phase 7).

### `Application/DependencyInjection.cs`
```csharp
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    services.AddScoped<ICommandHandler<CreateExampleCommand, Guid>, CreateExampleCommandHandler>();
    services.AddScoped<IQueryHandler<GetExampleQuery, ExampleResponse>, GetExampleQueryHandler>();
    return services;
}
```
- Lives in Application so it can register `internal sealed` handlers (same assembly).
- Manual registration today — explicit, no Scrutor dependency. When this file gets long, swap in assembly-scanning later.

### First Migration (5.7) — generated
```bash
dotnet ef migrations add Initial \
  --project src/Infrastructure \
  --startup-project src/Web.Api \
  --output-dir Database/Migrations \
  --context ApplicationDbContext
```
Produced three files under `src/Infrastructure/Database/Migrations/`:
- `20260511181635_Initial.cs` — the up/down operations
- `20260511181635_Initial.Designer.cs` — model snapshot for this migration
- `ApplicationDbContextModelSnapshot.cs` — the latest cumulative model snapshot

The migration creates a single `examples` table with `Id uuid PK` and `Name varchar(200) NOT NULL`. Matches `ExampleConfiguration.cs`.

## Tooling Decisions

### Local `dotnet-ef` tool
Created `.config/dotnet-tools.json` via `dotnet new tool-manifest`, then `dotnet tool install dotnet-ef --version 10.0.7`. Locks the EF Core CLI to the same version as the runtime package. Future contributors run `dotnet tool restore` once and migrations work; no global tool drift.

### EF Design package
Two references, both with `PrivateAssets=all`:
- `Microsoft.EntityFrameworkCore.Design` in **Infrastructure** — needed for the migration generator
- `Microsoft.EntityFrameworkCore.Design` in **Web.Api** — EF CLI inspects the startup project too

`PrivateAssets=all` means Design is build/dev-only — it never ships in deployment artifacts and doesn't leak to consuming projects.

### Nested `.editorconfig` for Migrations
EF's generator emits block-scoped namespaces and other style choices that fight our strict rules. Added `src/Infrastructure/Database/Migrations/.editorconfig` to relax the rules for generated files:
- `csharp_style_namespace_declarations = block_scoped:silent` (IDE0161)
- `dotnet_diagnostic.CA1814.severity = none` (multidimensional arrays in seed data)
- `dotnet_diagnostic.IDE0058.severity = none` (expression value never used — chained builders)

The host `.editorconfig` stays strict for human-written code.

## Architecture Tests — Status

11/11 still passing. **No new layer assertions needed**: Web.Api may depend on every inner layer; the rule chain we built in Phases 2–5 already covers the reverse direction:
- Domain ⊄ {Application, Infrastructure, Web.Api, frameworks}
- Application ⊄ {Infrastructure, Web.Api, web frameworks}
- Infrastructure ⊄ {Web.Api, web frameworks}

## Trade-offs Logged

| Where | We chose | Trade-off |
|---|---|---|
| Endpoints implement `IEndpoint`, no controllers | Minimal API + auto-discovery; tiny `Program.cs` | No `[Route]`/`[HttpGet]` attribute family |
| Handlers injected by interface | One fewer abstraction (no mediator) | Each endpoint declares the handler type it wants |
| `Result<T>` → `IResult` in one extension | All status mapping in one place | Anonymous error payload (no ProblemDetails contract) |
| Modern `IExceptionHandler` | Recommended .NET 8+ approach | Older middleware patterns still common in tutorials |
| Manual handler DI registration | Explicit, no Scrutor dep | Add a line per new handler — swap to scanning later |
| `dotnet-ef` as local tool | Pinned version, reproducible | One extra `dotnet tool restore` step for new contributors |
| Nested `.editorconfig` in Migrations | Generated files don't fight house style | One more `.editorconfig` to know about |
| Anonymous error JSON | Simple, no extra types | Doesn't follow RFC 7807 ProblemDetails strictly |
| `Microsoft.EntityFrameworkCore.Design` in two projects | EF CLI works | Two `PackageReference` to manage; both `PrivateAssets=all` |

## Build Surprises

Two more analyzer rules silenced:
| Rule | Why |
|---|---|
| CA1062 | NRT enforces non-null at the type level; double-validating is redundant |
| CA1848 | `LoggerMessage` source generators are for hot-path logging; manual `_logger.LogError` is clearer for exception handlers |

The MSB3277 warning (EF Core 10.0.4 ↔ 10.0.7 version unification) is informational — Npgsql 10.0.1 pulls EF Core 10.0.4 transitively while our explicit reference is 10.0.7. Build succeeds; runtime uses 10.0.7. Resolve later by aligning Npgsql once a 10.0.7-aligned release ships.

**Twelve analyzer suppressions total**, each with a `# Reason:` comment.

## Lessons That Generalize

1. **Endpoints are plugins.** Implement `IEndpoint`, get auto-registered. Program.cs has no list of routes.
2. **Result → IResult is one function.** Centralize HTTP status mapping; never let an endpoint hand-roll the same switch.
3. **`IExceptionHandler` over middleware** in .NET 8+. Modern hook, easier to test.
4. **Tool manifests pin everything.** A repo with `.config/dotnet-tools.json` works the same on any contributor's machine.
5. **Generated code gets its own `.editorconfig`.** Don't fight the generator; relax the rules for its output.
6. **The migration is *part of the architecture*, not an afterthought.** Generate it once Web.Api exists; commit the files; treat them as source.
7. **Bootstrap should fit on one screen.** If Program.cs grows past ~20 lines, you're missing an extension method somewhere.

## Verification

```
$ dotnet build CleanArchitect.slnx
Build succeeded.   0 Error(s)   1 informational MSBuild warning (EF version unification)

$ dotnet test tests/ArchitectureTests/
Passed!  -  Failed: 0, Passed: 11, Skipped: 0, Total: 11, Duration: 57 ms

$ ls src/Infrastructure/Database/Migrations/
20260511181635_Initial.cs
20260511181635_Initial.Designer.cs
ApplicationDbContextModelSnapshot.cs
```

### Running locally (requires Postgres)
```bash
# Bring up Postgres any way you like — e.g. docker
docker run --rm -d -p 5432:5432 \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=CleanArchitect \
  --name clean-architect-db postgres:17

# Apply the migration
dotnet ef database update \
  --project src/Infrastructure \
  --startup-project src/Web.Api \
  --context ApplicationDbContext

# Run the API
dotnet run --project src/Web.Api

# In another shell:
curl -X POST http://localhost:5000/examples \
  -H 'Content-Type: application/json' \
  -d '{"name":"widget"}'
# → 201 Created, Location: /examples/<guid>, body { "id": "<guid>" }

curl http://localhost:5000/examples/<that-guid>
# → 200 OK { "id": "...", "name": "widget" }

curl http://localhost:5000/examples/$(uuidgen)
# → 404 Not Found { "code": "Example.NotFound", "description": "..." }
```

## What's Next

Phase 7 — Test scaffolding. We'll create `tests/Application.UnitTests` (NSubstitute + Shouldly + MockQueryable.NSubstitute for `DbSet` mocking) and `tests/Api.IntegrationTests` (Testcontainers Postgres + `WebApplicationFactory<Program>` hitting the API via HTTP only). Extend `ArchitectureTests` with handler-convention assertions (must be `internal sealed`, name ends with `CommandHandler`/`QueryHandler`, return `Task<Result>` or `Task<Result<T>>`).
