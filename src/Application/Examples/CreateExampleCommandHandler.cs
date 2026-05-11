using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Examples;
using SharedKernel;

namespace Application.Examples;

internal sealed class CreateExampleCommandHandler(IApplicationDbContext dbContext)
    : ICommandHandler<CreateExampleCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        CreateExampleCommand command,
        CancellationToken cancellationToken)
    {
        Result<Example> result = Example.Create(command.Name);
        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }

        dbContext.Examples.Add(result.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return result.Value.Id;
    }
}
