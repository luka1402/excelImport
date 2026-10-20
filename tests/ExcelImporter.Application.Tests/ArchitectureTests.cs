using NetArchTest.Rules;
using Shouldly;
using System.Reflection;

namespace ExcelImporter.Application.Tests;

public class ArchitectureTests
{
    private const string DomainNamespace = "ExcelImporter.Domain";
    private const string ApplicationNamespace = "ExcelImporter.Application";
    private const string InfrastructureNamespace = "ExcelImporter.Infrastructure";
    private const string ApiNamespace = "ExcelImporter.Api";

    private static readonly Assembly DomainAssembly = ExcelImporter.Domain.AssemblyReference.Assembly;
    private static readonly Assembly ApplicationAssembly = ExcelImporter.Application.AssemblyReference.Assembly;
    private static readonly Assembly InfrastructureAssembly = ExcelImporter.Infrastructure.AssemblyReference.Assembly;
    private static readonly Assembly ApiAssembly = ExcelImporter.Api.AssemblyReference.Assembly;

    [Fact]
    public void Domain_ShouldNotDependOnAnyOtherLayer()
    {
        AssertNoDependency(DomainAssembly, ApplicationNamespace, InfrastructureNamespace, ApiNamespace);
        ReferencedProjects(DomainAssembly).ShouldBeEmpty();
    }

    [Fact]
    public void Application_ShouldDependOnlyOnDomain()
    {
        AssertNoDependency(ApplicationAssembly, InfrastructureNamespace, ApiNamespace);
        ReferencedProjects(ApplicationAssembly).ShouldBeSubsetOf([DomainNamespace]);
    }

    [Fact]
    public void Infrastructure_ShouldNotDependOnApi()
    {
        AssertNoDependency(InfrastructureAssembly, ApiNamespace);
        ReferencedProjects(InfrastructureAssembly).ShouldBeSubsetOf([DomainNamespace, ApplicationNamespace]);
    }

    [Fact]
    public void Api_ShouldNotBeReferencedByInnerLayers()
    {
        foreach (var assembly in new[] { DomainAssembly, ApplicationAssembly, InfrastructureAssembly })
        {
            ReferencedProjects(assembly).ShouldNotContain(ApiNamespace);
        }
    }

    [Fact]
    public void Api_ShouldReferenceOnlyApplicationAndInfrastructureProjects()
    {
        ReferencedProjects(ApiAssembly).ShouldBeSubsetOf([DomainNamespace, ApplicationNamespace, InfrastructureNamespace]);
    }

    [Fact]
    public void Rule_ShouldFail_WhenDependencyIsViolated()
    {
        // Kontrolni slučaj: ovaj test assembly koristi Application, pa pravilo mora da ga označi kao prekršeno.
        var result = Types.InAssembly(typeof(ArchitectureTests).Assembly)
            .That().ResideInNamespace("ExcelImporter.Application.Tests")
            .ShouldNot().HaveDependencyOn(ApplicationNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeFalse();
    }

    private static void AssertNoDependency(Assembly assembly, params string[] forbiddenNamespaces)
    {
        var result = Types.InAssembly(assembly)
            .ShouldNot().HaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"Prekršeno pravilo zavisnosti: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    private static IEnumerable<string> ReferencedProjects(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.StartsWith("ExcelImporter.", StringComparison.Ordinal));
}

