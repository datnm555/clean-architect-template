using Application.Abstractions.Messaging;
using Application.Examples.Data;

namespace Application.Examples;

public sealed record GetExampleQuery(Guid Id) : IQuery<ExampleResponse>;
