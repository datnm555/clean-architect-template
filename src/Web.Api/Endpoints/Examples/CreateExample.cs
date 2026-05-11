using Application.Abstractions.Messaging;
using Application.Examples;
using SharedKernel;
using Web.Api.Infrastructure;
using Web.Api.Middleware;

namespace Web.Api.Endpoints.Examples;

internal sealed class CreateExample : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/examples", async (
            CreateExampleCommand command,
            ICommandHandler<CreateExampleCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.ToHttpResult(id => Results.Created($"/examples/{id}", new { id }));
        });
    }
}
