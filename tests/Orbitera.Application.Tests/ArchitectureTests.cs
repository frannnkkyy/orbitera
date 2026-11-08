using System.Xml.Linq;

namespace Orbitera.Application.Tests;

public class ArchitectureTests
{
    [Fact]
    public void ApplicationProjectReferencesDomainButNotOuterLayersOrWpf()
    {
        var solutionDirectory = FindSolutionDirectory();
        var projectPath = Path.Combine(
            solutionDirectory,
            "src",
            "Orbitera.Application",
            "Orbitera.Application.csproj");
        var project = XDocument.Load(projectPath);
        var references = project.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => include is not null)
            .Select(include => Path.GetFileNameWithoutExtension(include))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var enablesWpf = project.Descendants()
            .Where(element => element.Name.LocalName == "UseWPF")
            .Any(element => bool.TryParse(element.Value, out var value) && value);

        Assert.Contains("Orbitera.Domain", references);
        Assert.DoesNotContain("Orbitera.Infrastructure", references);
        Assert.DoesNotContain("Orbitera.App", references);
        Assert.False(enablesWpf);
    }

    private static string FindSolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Orbitera.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find Orbitera.sln above the test output directory.");
    }
}
