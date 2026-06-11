namespace GazeStick.Models;

public readonly record struct GazePoint(double X, double Y)
{
    public static GazePoint Invalid => new(double.NaN, double.NaN);
    public bool IsValid => !double.IsNaN(X) && !double.IsNaN(Y);
}