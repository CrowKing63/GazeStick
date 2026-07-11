// Tests for circular normalization fix (v1.1.1+).
//
// Three algorithms compared:
//   BASELINE = v1.0.2 reference: raw dx/dy, no circular norm (axis clamp only)
//   OLD      = v1.1.0 buggy:     nxRaw/nyRaw distance, circular norm at end
//   NEW      = v1.1.1 fix:       raw dx/dy, circular norm at end
//
// Pass criteria:
//   - Cardinal direction: NEW must match BASELINE exactly (same sensitivity)
//   - Diagonal direction: NEW magnitude must be <= 1.0 (capped)

static (double nx, double ny, double mag) MapV102(double gazeX, double gazeY,
    double deadzone, double sensitivity)
{
    double dx = gazeX - 0.5;
    double dy = gazeY - 0.5;

    double distance = Math.Sqrt(dx * dx + dy * dy);
    deadzone = Math.Clamp(deadzone, 0.0, 0.5);

    if (distance < deadzone)
        return (0.0, 0.0, 0.0);

    double scale = (distance - deadzone) / (1.0 - deadzone);

    double nx = (dx / distance) * scale;
    double ny = (dy / distance) * scale;

    sensitivity = Math.Clamp(sensitivity, 0.1, 5.0);
    nx = Math.Clamp(nx * sensitivity, -1.0, 1.0);
    ny = Math.Clamp(ny * sensitivity, -1.0, 1.0);

    double mag = Math.Sqrt(nx * nx + ny * ny);
    return (nx, ny, mag);
}

static (double nx, double ny, double mag) MapNew(double gazeX, double gazeY,
    double deadzone, double sensitivity)
{
    double dx = gazeX - 0.5;
    double dy = gazeY - 0.5;

    double distance = Math.Sqrt(dx * dx + dy * dy);
    deadzone = Math.Clamp(deadzone, 0.0, 0.5);

    if (distance < deadzone)
        return (0.0, 0.0, 0.0);

    double scale = (distance - deadzone) / (1.0 - deadzone);

    double nx = (dx / distance) * scale;
    double ny = (dy / distance) * scale;

    sensitivity = Math.Clamp(sensitivity, 0.1, 5.0);
    nx = Math.Clamp(nx * sensitivity, -1.0, 1.0);
    ny = Math.Clamp(ny * sensitivity, -1.0, 1.0);

    double mag = Math.Sqrt(nx * nx + ny * ny);
    if (mag > 1.0)
    {
        nx = nx / mag;
        ny = ny / mag;
    }

    mag = Math.Sqrt(nx * nx + ny * ny);
    return (nx, ny, mag);
}

double deadzone = 0.10;
double[] sensitivities = [1.0, 2.0, 3.0];
double eps = 1e-9;

Console.WriteLine("=== Circular Normalization Fix Verification ===");
Console.WriteLine($"Deadzone: {deadzone}");
Console.WriteLine();
Console.WriteLine("Algorithms:");
Console.WriteLine("  BASELINE = v1.0.2 reference (raw dx/dy, axis clamp only)");
Console.WriteLine("  NEW      = fixed (raw dx/dy, circular norm at end)");
Console.WriteLine();
Console.WriteLine("Criteria:");
Console.WriteLine("  1) Cardinal: NEW nx/ny must MATCH BASELINE exactly (same sensitivity)");
Console.WriteLine("  2) Diagonal:  NEW magnitude must be <= 1.0 (circular clamp works)");
Console.WriteLine();

// ── Test points ──
var points = new List<(string label, double gx, double gy, bool isDiag)>
{
    // Cardinal
    ("Center",      0.500, 0.500, false),
    ("Right edge",  1.000, 0.500, false),
    ("Left edge",   0.000, 0.500, false),
    ("Top edge",    0.500, 0.000, false),
    ("Bottom edge", 0.500, 1.000, false),
    // Mid-cardinal (specific dx/dy values for accuracy check)
    ("R=0.7",       0.700, 0.500, false),
    ("R=0.8",       0.800, 0.500, false),
    ("R=0.9",       0.900, 0.500, false),
    // Diagonal
    ("Top-Left",    0.000, 0.000, true),
    ("Top-Right",   1.000, 0.000, true),
    ("Bottom-Left", 0.000, 1.000, true),
    ("Bottom-Right",1.000, 1.000, true),
    // Off-diagonal (asymmetric)
    ("R=0.9,T=0.7", 0.900, 0.300, true),
    ("R=0.8,T=0.6", 0.800, 0.400, true),
};

int passCardinal = 0, failCardinal = 0;
int passDiagonal = 0, failDiagonal = 0;

foreach (var sens in sensitivities)
{
    Console.WriteLine($"── Sensitivity = {sens} ───────────────────────────────");
    Console.WriteLine($"{"Point",-14} {"BASE nx",10} {"BASE ny",10} {"BASE mag",10}   {"NEW nx",10} {"NEW ny",10} {"NEW mag",10}   {"Status"}");
    Console.WriteLine(new string('─', 100));

    foreach (var (label, gx, gy, isDiag) in points)
    {
        var baseR = MapV102(gx, gy, deadzone, sens);
        var newR  = MapNew(gx, gy, deadzone, sens);

        string status;
        if (!isDiag)
        {
            bool sameNx = Math.Abs(baseR.nx - newR.nx) < eps;
            bool sameNy = Math.Abs(baseR.ny - newR.ny) < eps;
            if (sameNx && sameNy)
            {
                status = "PASS cardinal";
                passCardinal++;
            }
            else
            {
                status = "FAIL CARDINAL MISMATCH";
                failCardinal++;
            }
        }
        else
        {
            bool capped = newR.mag <= 1.0001;
            status = capped ? "PASS diagonal capped" : "FAIL DIAGONAL NOT CAPPED";
            if (capped) passDiagonal++; else failDiagonal++;
        }

        Console.WriteLine($"{label,-14} {baseR.nx,10:F6} {baseR.ny,10:F6} {baseR.mag,10:F6}   {newR.nx,10:F6} {newR.ny,10:F6} {newR.mag,10:F6}   {status}");
    }
    Console.WriteLine();
}

Console.WriteLine("═══ SUMMARY ═══");
Console.WriteLine($"Cardinal direction tests: {passCardinal} passed, {failCardinal} failed");
Console.WriteLine($"Diagonal direction tests: {passDiagonal} passed, {failDiagonal} failed");

bool allPass = failCardinal == 0 && failDiagonal == 0;
Console.WriteLine($"Overall: {(allPass ? "ALL PASS" : "FAILURES DETECTED")}");

// ── Deadzone diagnostic ──
Console.WriteLine();
Console.WriteLine("═══ DEADZONE DIAGNOSTIC ═══");
Console.WriteLine("Pixel distance from center at deadzone exit (16:9 screen 1920x1080).");
Console.WriteLine("Distance is computed from raw dx, dy (v1.0.2 / v1.1.1 formula).");
Console.WriteLine();

int screenW = 1920, screenH = 1080;

// distance = sqrt(dx*dx + dy*dy) = deadzone
// For cardinal: |dx| = deadzone, dy = 0
double dxRight = deadzone;
double pxRight = dxRight * screenW;
Console.WriteLine($"Deadzone setting: {deadzone}");
Console.WriteLine($"Screen: {screenW}x{screenH} (aspect {screenW / (double)screenH:F4})");
Console.WriteLine();

Console.WriteLine($"Right edge deadzone exit: dx = {dxRight:F4}, pixels from center = {pxRight:F1}px");

double dyTop = deadzone;
double pxTop = dyTop * screenH;
Console.WriteLine($"Top edge deadzone exit:    dy = {dyTop:F4}, pixels from center = {pxTop:F1}px");

double ratio = pxRight / pxTop;
Console.WriteLine();
Console.WriteLine($"=> Horizontal deadzone is {ratio:F2}x larger than vertical in physical pixels.");
Console.WriteLine($"   Both axes use same deadzone value ({deadzone}) in normalized space,");
Console.WriteLine($"   but the screen is wider ({screenW}px) than tall ({screenH}px).");
Console.WriteLine();
Console.WriteLine("For a physically circular deadzone (same pixel radius on 16:9),");
Console.WriteLine("consider aspect-aware normalization in a future version.");

return allPass ? 0 : 1;
