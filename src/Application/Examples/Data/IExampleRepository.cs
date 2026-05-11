namespace Application.Examples.Data;

public interface IExampleRepository
{
    Task<ExampleResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
