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

    // Application may depend on EF Core (DbSet&lt;T&gt;); it must not depend on web hosting.
    private static readonly string[] WebFrameworkNamespaces =
    [
        "Microsoft.AspNetCore",
        "Microsoft.Extensions.Hosting"
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

    [Fact]
    public void Domain_Should_NotDependOn_Application()
    {
        TestResult result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOn("Application")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            BuildFailureMessage(result, "Domain must not reference Application"));
    }

    [Fact]
    public void Domain_Should_NotDependOn_Infrastructure()
    {
        TestResult result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOn("Infrastructure")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            BuildFailureMessage(result, "Domain must not reference Infrastructure"));
    }

    [Fact]
    public void Domain_Should_NotDependOn_WebApi()
    {
        TestResult result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOn("Web.Api")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            BuildFailureMessage(result, "Domain must not reference Web.Api"));
    }

    [Fact]
    public void Domain_Should_NotDependOn_Frameworks()
    {
        TestResult result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny(FrameworkNamespaces)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            BuildFailureMessage(result, "Domain must stay framework-free"));
    }

    [Fact]
    public void Application_Should_NotDependOn_Infrastructure()
    {
        TestResult result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOn("Infrastructure")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            BuildFailureMessage(result, "Application must not reference Infrastructure"));
    }

    [Fact]
    public void Application_Should_NotDependOn_WebApi()
    {
        TestResult result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOn("Web.Api")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            BuildFailureMessage(result, "Application must not reference Web.Api"));
    }

    [Fact]
    public void Application_Should_NotDependOn_WebFrameworks()
    {
        TestResult result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny(WebFrameworkNamespaces)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            BuildFailureMessage(result, "Application must not depend on ASP.NET or hosting"));
    }

    private static string BuildFailureMessage(TestResult result, string rule)
    {
        var offenders = result.FailingTypes is null
            ? "(no type list available)"
            : string.Join(", ", result.FailingTypes.Select(t => t.FullName));
        return $"{rule}. Offending types: {offenders}";
    }
}
