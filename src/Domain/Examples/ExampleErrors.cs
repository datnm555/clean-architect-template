using SharedKernel;

namespace Domain.Examples;

public static class ExampleErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Example.NotFound",
        "The requested example was not found.");

    public static readonly Error NameRequired = Error.Validation(
        "Example.NameRequired",
        "Example name is required.");

    public static Error NameTooLong(int actualLength) => Error.Validation(
        "Example.NameTooLong",
        $"Example name must be at most {SystemConstants.NameMaxLength} characters (got {actualLength}).");
}
