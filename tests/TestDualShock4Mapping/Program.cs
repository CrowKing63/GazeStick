using GazeStick.Services;

var cases = new (string Name, byte Actual, byte Expected)[]
{
    ("X neutral", VirtualPadAxisConverter.ToDualShock4X(0), 128),
    ("X minimum", VirtualPadAxisConverter.ToDualShock4X(short.MinValue), 0),
    ("X maximum", VirtualPadAxisConverter.ToDualShock4X(short.MaxValue), 255),
    ("Y neutral", VirtualPadAxisConverter.ToDualShock4Y(0), 128),
    ("Y normal (XInput up)", VirtualPadAxisConverter.ToDualShock4Y(short.MaxValue), 0),
    ("Y normal (XInput down)", VirtualPadAxisConverter.ToDualShock4Y(short.MinValue), 255),
    ("Y inverted (XInput up)", VirtualPadAxisConverter.ToDualShock4Y(short.MinValue), 255),
};

var failures = cases.Where(test => test.Actual != test.Expected).ToList();
foreach (var test in cases)
    Console.WriteLine($"{test.Name}: {test.Actual} {(test.Actual == test.Expected ? "PASS" : $"FAIL (expected {test.Expected})")}");

return failures.Count == 0 ? 0 : 1;
