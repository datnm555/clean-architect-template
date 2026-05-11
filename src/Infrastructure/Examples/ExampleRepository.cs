using Application.Abstractions.Data;
using Application.Examples.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Examples;

internal sealed class ExampleRepository(IApplicationDbContext dbContext) : IExampleRepository
{
    public Task<ExampleResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Examples
            .Where(e => e.Id == id)
            .Select(e => new ExampleResponse(e.Id, e.Name))
            .FirstOrDefaultAsync(cancellationToken);
}
