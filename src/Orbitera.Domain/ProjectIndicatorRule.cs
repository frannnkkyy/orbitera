namespace Orbitera.Domain;

public sealed record ProjectIndicatorRule(
    string FileName,
    DevelopmentEcosystem Ecosystem,
    bool IsDirectory = false);
