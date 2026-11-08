namespace Orbitera.Domain;

public readonly record struct TreemapBounds(double X, double Y, double Width, double Height)
{
    public double Area => Width * Height;
}
