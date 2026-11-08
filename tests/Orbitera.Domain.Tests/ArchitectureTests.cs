using System.Reflection;

namespace Orbitera.Domain.Tests;

public class ArchitectureTests
{
    [Fact]
    public void DomainAssemblyDoesNotReferenceOuterLayersOrWpf()
    {
        var references = Assembly.Load("Orbitera.Domain")
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("Orbitera.Application", references);
        Assert.DoesNotContain("Orbitera.Infrastructure", references);
        Assert.DoesNotContain("Orbitera.App", references);
        Assert.DoesNotContain("PresentationFramework", references);
    }
}
