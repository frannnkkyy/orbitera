namespace Orbitera.Domain;

public sealed record DetectedDevelopmentProject(
    string Name,
    string RootPath,
    IReadOnlySet<DevelopmentEcosystem> Ecosystems,
    IReadOnlyList<string> Indicators);
