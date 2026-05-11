using Application.Abstractions.Messaging;
using Application.Examples.Data;
using Domain.Examples;
using SharedKernel;

namespace Application.Examples;

internal sealed class GetExampleQueryHandler(IExampleRepository repository)
    : IQueryHandler<GetExampleQuery, ExampleResponse>
{
    public async Task<Result<ExampleResponse>> Handle(
        GetExampleQuery query,
        CancellationToken cancellationToken)
    {
        ExampleResponse? response = await repository.GetByIdAsync(query.Id, cancellationToken);
        if (response is null)
        {
            return Result.Failure<ExampleResponse>(ExampleErrors.NotFound);
        }

        return response;
    }
}
