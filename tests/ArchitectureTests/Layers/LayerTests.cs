using NetArchTest.Rules;
using Shouldly;

namespace ArchitectureTests.Layers;

/// <summary>
/// Compiler-enforced Dependency Rule. New assertions are added as each layer is created.
/// </summary>
public class LayerTests : BaseTest
{
    private static readonly string[] OuterLayerNamespaces =
    [
        "Domain",
        "Application",
        "Infrastructure",
        "Web.Api"
    ];

    private static readonly string[] FrameworkNamespaces =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Microsoft.Extensions.Hosting",
        "Microsoft.Extensions.Logging"
    ];

    [Fact]
    public void SharedKernel_Should_NotDependOn_AnyOuterLayer()
    {
        TestResult result = Types.InAssembly(SharedKernelAssembly)
            .Should()
            .NotHaveDependencyOnAny(OuterLayerNamespaces)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            BuildFailureMessage(result, "SharedKernel must not reference outer layers"));
    }

    [Fact]
    public void SharedKernel_Should_NotDependOn_Frameworks()
    {
        TestResult result = Types.InAssembly(SharedKernelAssembly)
            .Should()
            .NotHaveDependencyOnAny(FrameworkNamespaces)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            BuildFailureMessage(result, "SharedKernel must stay framework-free"));
    }

    private static string BuildFailureMessage(TestResult result, string rule)
    {
        var offenders = result.FailingTypes is null
            ? "(no type list available)"
            : string.Join(", ", result.FailingTypes.Select(t => t.FullName));
        return $"{rule}. Offending types: {offenders}";
    }
}
