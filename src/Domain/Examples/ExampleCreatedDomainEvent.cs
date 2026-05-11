using SharedKernel;

namespace Domain.Examples;

public sealed record ExampleCreatedDomainEvent(Guid ExampleId, string Name) : IDomainEvent;
