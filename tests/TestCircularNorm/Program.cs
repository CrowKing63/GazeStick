// Replicates StickMapper.Map() math for before/after comparison
// Tests: center, 4 cardinal edges, 4 corners at sensitivity 1.0, 2.0, 3.0

static (double nx, double ny, double mag) MapGaze(double gazeX, double gazeY,
    double deadzone, double sensitivity, bool useCircularNorm)
{
    double dx = gazeX - 0.5;
    double dy = gazeY - 0.5;

    const double halfWidth = 0.5;
    const double halfHeight = 0.5;

    double nxRaw = dx / halfWidth;
    double nyRaw = dy / halfHeight;

    double distance = Math.Sqrt(nxRaw * nxRaw + nyRaw * nyRaw);
    deadzone = Math.Clamp(deadzone, 0.0, 0.5);

    if (distance < deadzone)
        return (0.0, 0.0, 0.0);

    double scale = (distance - deadzone) / (1.0 - deadzone);

    double nx = (nxRaw / distance) * scale;
    double ny = (nyRaw / distance) * scale;

    sensitivity = Math.Clamp(sensitivity, 0.1, 5.0);
    nx = Math.Clamp(nx * sensitivity, -1.0, 1.0);
    ny = Math.Clamp(ny * sensitivity, -1.0, 1.0);

    if (useCircularNorm)
    {
        double m = Math.Sqrt(nx * nx + ny * ny);
        if (m > 1.0)
        {
            nx = nx / m;
            ny = ny / m;
        }
    }

    double mag = Math.Sqrt(nx * nx + ny * ny);
    return (nx, ny, mag);
}

double deadzone = 0.10;
double[] sensitivities = [1.0, 2.0, 3.0];

// For a given sensitivity, compute the gaze coordinate on the bottom-right
// diagonal where the axis clamp just begins to trigger (|nx| = 1.0 after sensitivity).
static (double gx, double gy) CalcBoundaryPoint(double sens, double dz)
{
    double sqrt2 = Math.Sqrt(2.0);
    double d = (dz + sqrt2 * (1.0 - dz) / sens) / (2.0 * sqrt2);
    return (0.5 + d, 0.5 + d);
}

// Test points: (label, gazeX, gazeY, isDiagonal)
var points = new List<(string label, double gx, double gy, bool isDiag)>
{
    ("Center",      0.500, 0.500, false),
    ("Right edge",  1.000, 0.500, false),
    ("Left edge",   0.000, 0.500, false),
    ("Top edge",    0.500, 0.000, false),
    ("Bottom edge", 0.500, 1.000, false),
    ("Top-Left",    0.000, 0.000, true),
    ("Top-Right",   1.000, 0.000, true),
    ("Bottom-Left", 0.000, 1.000, true),
    ("Bottom-Right",1.000, 1.000, true),
};

// Add auto-generated boundary points for each sensitivity
foreach (var sens in sensitivities)
{
    var (gx, gy) = CalcBoundaryPoint(sens, deadzone);
    // Clamp to valid gaze range [0, 1]
    gx = Math.Clamp(gx, 0.0, 1.0);
    gy = Math.Clamp(gy, 0.0, 1.0);
    points.Add(($"Boundary(S={sens})", gx, gy, true));
}

Console.WriteLine("=== Circular Normalization Test ===");
Console.WriteLine($"Deadzone: {deadzone}");
Console.WriteLine();
Console.WriteLine("Legend:");
Console.WriteLine("  OLD = without circular norm (axis clamp only)");
Console.WriteLine("  NEW = with magnitude clamp after sensitivity");
Console.WriteLine("  PASS = cardinal unchanged? (OLD mag ≈ NEW mag)  |  diagonal capped? (NEW mag ≤ 1.0)");
Console.WriteLine();

int passCardinal = 0, failCardinal = 0;
int passDiagonal = 0, failDiagonal = 0;

foreach (var sens in sensitivities)
{
    Console.WriteLine($"── Sensitivity = {sens} ───────────────────────────────");
    Console.WriteLine($"{"Point",-14} {"OLD nx",12} {"OLD ny",12} {"OLD mag",12}   {"NEW nx",12} {"NEW ny",12} {"NEW mag",12}   {"Status"}");
    Console.WriteLine(new string('─', 105));

    foreach (var (label, gx, gy, isDiag) in points)
    {
        var oldR = MapGaze(gx, gy, deadzone, sens, useCircularNorm: false);
        var newR = MapGaze(gx, gy, deadzone, sens, useCircularNorm: true);

        bool cardinalOk = !isDiag || Math.Abs(oldR.mag - newR.mag) < 1e-9;
        bool diagOk = !isDiag || newR.mag <= 1.0001;

        string status = "";
        if (!isDiag)
        {
            bool sameMag = Math.Abs(oldR.mag - newR.mag) < 1e-9;
            status = sameMag ? "✓ cardinal" : "✗ CARDINAL CHANGED";
            if (sameMag) passCardinal++; else failCardinal++;
        }
        else
        {
            bool capped = newR.mag <= 1.0001;
            bool sameDir = Math.Abs(newR.mag - 1.0) < 1e-6;
            status = capped ? (sameDir ? "✓ capped@1.0" : "✓ mag<1.0") : "✗ NOT CAPPED";
            if (capped) passDiagonal++; else failDiagonal++;
        }

        Console.WriteLine($"{label,-14} {oldR.nx,12:F6} {oldR.ny,12:F6} {oldR.mag,12:F6}   {newR.nx,12:F6} {newR.ny,12:F6} {newR.mag,12:F6}   {status}");
    }
    Console.WriteLine();
}

Console.WriteLine("═══ SUMMARY ═══");
Console.WriteLine($"Cardinal direction tests: {passCardinal} passed, {failCardinal} failed");
Console.WriteLine($"Diagonal direction tests: {passDiagonal} passed, {failDiagonal} failed");

bool allPass = failCardinal == 0 && failDiagonal == 0;
Console.WriteLine($"Overall: {(allPass ? "ALL PASS" : "FAILURES DETECTED")}");

Console.WriteLine();
Console.WriteLine("═══ ASPECT RATIO DIAGNOSTIC ═══");
Console.WriteLine("Checks whether the deadzone is physically circular on a 16:9 screen (1920×1080).");
Console.WriteLine();

int screenW = 1920, screenH = 1080;
double halfW = 0.5, halfH = 0.5;

// At deadzone exit in each cardinal direction, the normalized distance = deadzone.
// Compute the gaze delta and corresponding pixel distance.
Console.WriteLine($"Deadzone setting: {deadzone}");
Console.WriteLine($"halfWidth = {halfW}, halfHeight = {halfH}");
Console.WriteLine($"Screen: {screenW}×{screenH} (aspect {screenW/(double)screenH:F4})");
Console.WriteLine();

// Right edge: pure horizontal, nyRaw = 0
// distance = |nxRaw| = deadzone  →  nxRaw = deadzone  →  dx = nxRaw * halfW = deadzone * halfW
// pixelDistX = dx * screenW
double dx_right = deadzone * halfW;
double pxRight = dx_right * screenW;
Console.WriteLine($"Right edge deadzone exit:  dx = {dx_right:F4},  pixels from center = {pxRight:F1}px");

// Top edge: pure vertical, nxRaw = 0
// distance = |nyRaw| = deadzone  →  nyRaw = deadzone  →  dy = nyRaw * halfH = deadzone * halfH
double dy_top = deadzone * halfH;
double pxTop = dy_top * screenH;
Console.WriteLine($"Top edge deadzone exit:    dy = {dy_top:F4},  pixels from center = {pxTop:F1}px");

double ratio = pxRight / pxTop;
Console.WriteLine();
Console.WriteLine($"=> Horizontal deadzone is {ratio:F2}× larger than vertical in physical pixels.");
Console.WriteLine($"   This is because both axes use the same halfWidth=halfHeight={halfW},");
Console.WriteLine($"   but the screen is wider ({screenW}px) than tall ({screenH}px).");
Console.WriteLine();
Console.WriteLine("For a physically circular deadzone (same pixel radius in all directions),");
Console.WriteLine($"halfWidth should be halfHeight × (screenWidth/screenHeight) = {halfH * screenW / screenH:F4}");
Console.WriteLine($"or equivalently: distance should use aspect-compensated coordinates.");
return allPass ? 0 : 1;
