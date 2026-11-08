using Orbitera.Domain;

namespace Orbitera.Domain.Tests;

public class DevelopmentInsightsTests
{
    [Fact]
    public void DetectsProjectsFromTheirIndicatorFilesInsteadOfFolderNames()
    {
        var root = Directory("workspace");
        var dotnet = Directory("Orbitera");
        dotnet.AddChild(File("Orbitera.csproj", 1));
        dotnet.AddChild(File("Orbitera.sln", 1));
        root.AddChild(dotnet);
        var node = Directory("web-client");
        node.AddChild(File("package.json", 1));
        root.AddChild(node);
        var python = Directory("tools");
        python.AddChild(File("pyproject.toml", 1));
        root.AddChild(python);
        var plain = Directory("build");
        plain.AddChild(File("notes.txt", 1));
        root.AddChild(plain);

        var result = CreateAnalyzer().Analyze(root);

        Assert.Equal(3, result.Projects.Count);
        Assert.Contains(result.Projects, project =>
            project.Name == "Orbitera" && project.Ecosystems.Contains(DevelopmentEcosystem.DotNet));
        Assert.Contains(result.Projects, project =>
            project.Name == "web-client" && project.Ecosystems.Contains(DevelopmentEcosystem.NodeJs));
        Assert.Contains(result.Projects, project =>
            project.Name == "tools" && project.Ecosystems.Contains(DevelopmentEcosystem.Python));
        Assert.Empty(result.Recommendations);
    }

    [Fact]
    public void ClassifiesArtifactsOnlyWhenAnAppropriateProjectIsDetected()
    {
        var root = Directory("workspace");
        var dotnet = Directory("dotnet-app");
        dotnet.AddChild(File("app.csproj", 1));
        dotnet.AddChild(DirectoryWithFile("bin", "app.dll", 100));
        dotnet.AddChild(DirectoryWithFile("obj", "app.cache", 50));
        root.AddChild(dotnet);

        var node = Directory("web-app");
        node.AddChild(File("package.json", 1));
        node.AddChild(File("next.config.ts", 1));
        node.AddChild(DirectoryWithFile("node_modules", "package.json", 200));
        node.AddChild(DirectoryWithFile(".next", "BUILD_ID", 80));
        node.AddChild(DirectoryWithFile("dist", "index.js", 40));
        root.AddChild(node);

        var python = Directory("python-app");
        python.AddChild(File("pyproject.toml", 1));
        python.AddChild(DirectoryWithFile(".venv", "python.exe", 300));
        python.AddChild(DirectoryWithFile("__pycache__", "module.pyc", 30));
        python.AddChild(DirectoryWithFile(".pytest_cache", "nodeids", 10));
        root.AddChild(python);

        var git = Directory("repository");
        git.AddChild(DirectoryWithFile(".git", "HEAD", 500));
        root.AddChild(git);

        var result = CreateAnalyzer().Analyze(root);

        Assert.Equal(9, result.Recommendations.Count);
        Assert.Contains(result.Recommendations, item =>
            item.DirectoryName == "bin" && item.ProjectName == "dotnet-app");
        Assert.Contains(result.Recommendations, item =>
            item.DirectoryName == "node_modules" && item.ProjectName == "web-app");
        Assert.Contains(result.Recommendations, item =>
            item.DirectoryName == ".pytest_cache" && item.ProjectName == "python-app");

        var gitRecommendation = Assert.Single(result.Recommendations, item => item.DirectoryName == ".git");
        Assert.Equal(DevelopmentRiskLevel.High, gitRecommendation.RiskLevel);
        Assert.False(gitRecommendation.IsPotentiallyRecoverable);
        Assert.Contains("historial", gitRecommendation.DeletionImpact, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(810, result.PotentiallyRecoverableBytes);
    }

    [Fact]
    public void DoesNotClassifyAmbiguousBuildDirectoryOrNextOutputWithoutSpecificMarker()
    {
        var root = Directory("workspace");
        var node = Directory("node-project");
        node.AddChild(File("package.json", 1));
        node.AddChild(DirectoryWithFile("build", "user-data.json", 100));
        node.AddChild(DirectoryWithFile(".next", "BUILD_ID", 50));
        root.AddChild(node);

        var result = CreateAnalyzer().Analyze(root);

        Assert.Empty(result.Recommendations);
        Assert.Equal(0, result.PotentiallyRecoverableBytes);
    }

    [Fact]
    public void UsesTheNearestMatchingProjectForNestedProjects()
    {
        var root = Directory("workspace");
        var outer = Directory("outer");
        outer.AddChild(File("outer.csproj", 1));
        var inner = Directory("inner");
        inner.AddChild(File("package.json", 1));
        inner.AddChild(DirectoryWithFile("node_modules", "dependency.js", 25));
        outer.AddChild(inner);
        root.AddChild(outer);

        var result = CreateAnalyzer().Analyze(root);

        var dependency = Assert.Single(result.Recommendations);
        Assert.Equal("inner", dependency.ProjectName);
        Assert.Equal(DevelopmentEcosystem.NodeJs, dependency.Ecosystem);
    }

    [Fact]
    public void SupportsAdditionalIndicatorsAndArtifactRulesThroughConfiguration()
    {
        var rules = new DevelopmentInsightsRules(
            [
                new ProjectIndicatorRule("orbit.project", DevelopmentEcosystem.DotNet)
            ],
            [
                new DevelopmentInsightRule(
                    ".orbit-cache",
                    DevelopmentEcosystem.DotNet,
                    DevelopmentInsightCategory.Cache,
                    "Orbit build cache.",
                    "The build regenerates this cache.",
                    DevelopmentRiskLevel.Low,
                    IsPotentiallyRecoverable: true)
            ]);
        var project = Directory("custom-app");
        project.AddChild(File("orbit.project", 1));
        project.AddChild(DirectoryWithFile(".orbit-cache", "item", 42));
        var analyzer = CreateAnalyzer(rules);

        var result = analyzer.Analyze(project);

        var recommendation = Assert.Single(result.Recommendations);
        Assert.Equal(".orbit-cache", recommendation.DirectoryName);
        Assert.Equal(42, result.PotentiallyRecoverableBytes);
    }

    private static DevelopmentInsightsAnalyzer CreateAnalyzer(DevelopmentInsightsRules? rules = null)
    {
        var configuredRules = rules ?? new DevelopmentInsightsRules();
        return new DevelopmentInsightsAnalyzer(
            new DevelopmentProjectDetector(configuredRules),
            new DevelopmentArtifactClassifier(configuredRules),
            new DevelopmentInsightRecommendationFactory());
    }

    private static StorageNode Directory(string name) =>
        StorageNode.CreateDirectory(name, Path.Combine(@"C:\workspace", name));

    private static StorageNode DirectoryWithFile(string name, string fileName, long size)
    {
        var directory = Directory(name);
        directory.AddChild(File(fileName, size));
        return directory;
    }

    private static StorageNode File(string name, long size) =>
        StorageNode.CreateFile(name, Path.Combine(@"C:\workspace", name), size, Path.GetExtension(name));
}
