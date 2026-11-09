namespace Orbitera.Domain;

public sealed record OrbitScoreFactor(
    string Name,
    int Weight,
    int Score,
    string Explanation);
