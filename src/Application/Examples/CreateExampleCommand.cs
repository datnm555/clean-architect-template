using System.ComponentModel.DataAnnotations;
using Application.Abstractions.Messaging;

namespace Application.Examples;

public sealed record CreateExampleCommand : ICommand<Guid>
{
    [Required]
    [MinLength(1)]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;
}
