using System.Reflection;
using Application.Abstractions.Messaging;
using Domain.Examples;
using Infrastructure;
using SharedKernel;

namespace ArchitectureTests;

/// <summary>
/// Holds Assembly references for every layer the architecture tests inspect.
/// Each property is added when its layer is born (SharedKernel → Web.Api as phases progress).
/// </summary>
public abstract class BaseTest
{
    protected static readonly Assembly SharedKernelAssembly = typeof(Entity).Assembly;
    protected static readonly Assembly DomainAssembly = typeof(Example).Assembly;
    protected static readonly Assembly ApplicationAssembly = typeof(ICommand).Assembly;
    protected static readonly Assembly InfrastructureAssembly = typeof(DependencyInjection).Assembly;
}
