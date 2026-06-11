namespace GazeStick.Models;

public readonly record struct StickOutput(short X, short Y)
{
    public static StickOutput Neutral => new(0, 0);
}