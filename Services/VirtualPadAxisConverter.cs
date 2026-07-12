using GazeStick.Models;

namespace GazeStick.Services;

internal static class VirtualPadAxisConverter
{
    public static byte ToDualShock4X(short value) => ToDualShock4Axis(value, invert: false);

    // XInput uses positive Y for up; a HID/DS4 Y axis uses lower values for up.
    public static byte ToDualShock4Y(short value) => ToDualShock4Axis(value, invert: true);

    private static byte ToDualShock4Axis(short value, bool invert)
    {
        if (value == 0)
            return 128;

        double normalized = (value - short.MinValue) / (double)ushort.MaxValue;
        if (invert)
            normalized = 1.0 - normalized;

        return (byte)Math.Clamp(
            (int)Math.Round(normalized * byte.MaxValue, MidpointRounding.AwayFromZero),
            byte.MinValue,
            byte.MaxValue);
    }
}
