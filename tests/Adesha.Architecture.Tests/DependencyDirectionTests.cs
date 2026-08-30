using System.Reflection;

namespace Adesha.Architecture.Tests;

/// <summary>
/// Enforces the Master Prompt dependency direction. These tests FAIL if:
/// - Domain references anything in the solution,
/// - Application references anything beyond Domain,
/// - ServiceDefaults references Domain, Application, or a broker project,
/// - Domain/Application reference a concrete broker project or Infrastructure,
/// - anything references the AppHost.
/// </summary>
public class DependencyDirectionTests
{
    private static readonly Assembly Domain = typeof(Adesha.Domain.Orders.OrderStatus).Assembly;
    private static readonly Assembly Application = typeof(Adesha.Application.Configuration.AdeshaOptions).Assembly;
    private static readonly Assembly Infrastructure = typeof(Adesha.Infrastructure.Persistence.AdeshaDbContext).Assembly;
    private static readonly Assembly ServiceDefaults = typeof(Adesha.ServiceDefaults.Redaction.CredentialRedactor).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    private static string[] AdeshaReferencesOf(Assembly assembly) =>
        [.. assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("Adesha.", StringComparison.Ordinal))
            .Order()];

    [Fact]
    public void Domain_references_no_other_solution_project()
    {
        Assert.Empty(AdeshaReferencesOf(Domain));
    }

    [Fact]
    public void Application_references_only_Domain()
    {
        Assert.Equal(["Adesha.Domain"], AdeshaReferencesOf(Application));
    }

    [Fact]
    public void ServiceDefaults_references_no_solution_project()
    {
        Assert.Empty(AdeshaReferencesOf(ServiceDefaults));
    }

    [Fact]
    public void Infrastructure_references_only_Application_and_Domain()
    {
        Assert.All(AdeshaReferencesOf(Infrastructure),
            name => Assert.Contains(name, new[] { "Adesha.Application", "Adesha.Domain" }));
    }

    [Theory]
    [MemberData(nameof(AllAssemblies))]
    public void Nothing_references_the_AppHost(string assemblyName, Assembly assembly)
    {
        Assert.False(
            AdeshaReferencesOf(assembly).Any(n => n.Contains("AppHost", StringComparison.Ordinal)),
            $"{assemblyName} must not reference the AppHost.");
    }

    [Theory]
    [MemberData(nameof(CoreAssemblies))]
    public void Domain_and_Application_reference_no_broker_project(string assemblyName, Assembly assembly)
    {
        Assert.False(
            AdeshaReferencesOf(assembly).Any(n => n.StartsWith("Adesha.Brokers.", StringComparison.Ordinal)
                && n != "Adesha.Brokers.Abstractions"),
            $"{assemblyName} must not reference a concrete broker project.");
    }

    [Fact]
    public void Api_references_no_concrete_broker_types_outside_DI_composition()
    {
        // Concrete broker adapters may only be touched by DI wiring. Until broker projects
        // exist (Work Order 2+), the API must not reference any broker assembly at all
        // except the abstractions.
        Assert.False(
            AdeshaReferencesOf(Api).Any(n => n.StartsWith("Adesha.Brokers.", StringComparison.Ordinal)
                && n != "Adesha.Brokers.Abstractions"),
            "Only DI composition (reviewed manually) may use concrete broker projects.");
    }

    public static TheoryData<string, Assembly> AllAssemblies() => new()
    {
        { "Domain", Domain },
        { "Application", Application },
        { "Infrastructure", Infrastructure },
        { "ServiceDefaults", ServiceDefaults },
        { "Api", Api },
    };

    public static TheoryData<string, Assembly> CoreAssemblies() => new()
    {
        { "Domain", Domain },
        { "Application", Application },
    };
}
