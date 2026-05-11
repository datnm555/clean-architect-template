using SharedKernel;

namespace Domain.Examples;

public sealed class Example : AggregateRoot
{
    private Example() { }

    public string Name { get; private set; } = string.Empty;

    public static Result<Example> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Example>(ExampleErrors.NameRequired);
        }

        var trimmed = name.Trim();
        if (trimmed.Length > SystemConstants.NameMaxLength)
        {
            return Result.Failure<Example>(ExampleErrors.NameTooLong(trimmed.Length));
        }

        var example = new Example
        {
            Id = Guid.CreateVersion7(),
            Name = trimmed
        };

        example.Raise(new ExampleCreatedDomainEvent(example.Id, example.Name));

        return example;
    }

    public Result Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(ExampleErrors.NameRequired);
        }

        var trimmed = name.Trim();
        if (trimmed.Length > SystemConstants.NameMaxLength)
        {
            return Result.Failure(ExampleErrors.NameTooLong(trimmed.Length));
        }

        Name = trimmed;
        return Result.Success();
    }
}
