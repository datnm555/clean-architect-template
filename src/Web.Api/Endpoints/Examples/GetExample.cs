using Application.Abstractions.Messaging;
using Application.Examples;
using Application.Examples.Data;
using SharedKernel;
using Web.Api.Infrastructure;
using Web.Api.Middleware;

namespace Web.Api.Endpoints.Examples;

internal sealed class GetExample : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/examples/{id:guid}", async (
            Guid id,
            IQueryHandler<GetExampleQuery, ExampleResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<ExampleResponse> result = await handler.Handle(
                new GetExampleQuery(id),
                cancellationToken);

            return result.ToHttpResult();
        });
    }
}
