// Tests for the corner attenuation state machine (v1.1.2).
//
// Drives the real GazeStick.Services.CornerAttenuation with deterministic
// delta-time so the state transitions and gain behavior are verifiable without
// a live gaze source.
//
// Pass criteria:
//   1) Inside ellipse  -> state Inside, gain == 1.0 (steady)
//   2) Leaving ellipse  -> gain dips toward 1-DipDepth, then reaches Outside (0) past WINDOW_MS
//   3) Re-entry         -> from Outside, gain smooths 0 -> 1 (Entering -> Inside)
//   4) Interrupt        -> Entering interrupted by re-leave goes back to Leaving, gain carries over
//   5) Re-enter in window -> Leaving interrupted by re-entry goes to Entering, gain carries over

using GazeStick.Services;

const double Eps = 1e-6;
const double Dt = 16.0; // ~60 fps frame

int pass = 0, fail = 0;

void Check(bool ok, string label, string detail = "")
{
    if (ok) { pass++; Console.WriteLine($"  PASS  {label}"); }
    else { fail++; Console.WriteLine($"  FAIL  {label}  {detail}"); }
}

// We need to observe state too. CornerAttenuation does not expose state publicly,
// but tests run in the same assembly visibility (InternalsVisibleTo), so we add a
// tiny internal observer via a wrapper isn't available. To keep the production API
// clean, we assert on gain (the only externally meaningful output) plus the
// documented invariants of each transition.

Console.WriteLine("=== Corner Attenuation State Machine Verification ===");
Console.WriteLine($"Dt = {Dt} ms/frame, DipDepth = 0.3, WINDOW_MS = 130, EnterTau = 90");
Console.WriteLine();

// ── 1) Inside stays Inside, gain 1.0 ──
{
    Console.WriteLine("1) Inside ellipse (u=0.5, v=0.0):");
    var c = new CornerAttenuation();
    double g = 1.0;
    for (int i = 0; i < 20; i++) g = c.Compute(0.5, 0.0, Dt);
    Check(Math.Abs(g - 1.0) < Eps, "gain remains 1.0 inside ellipse", $"gain={g}");
}

// ── 2) Leaving then Outside ──
{
    Console.WriteLine("2) Leave ellipse (u=0.9, v=0.9) for ~10 frames (t ~160ms > 130):");
    var c = new CornerAttenuation();
    double g = 1.0;
    // first frame: Inside -> Leaving (gain stays 1.0 this frame)
    g = c.Compute(0.9, 0.9, Dt);
    Check(Math.Abs(g - 1.0) < Eps, "frame 1 of leave keeps gain 1.0 (dip starts next)", $"gain={g}");
    // subsequent frames dip
    for (int i = 0; i < 8; i++) g = c.Compute(0.9, 0.9, Dt);
    Check(g < 1.0 && g > 0.0, "gain dips while leaving", $"gain={g}");
    // continue until grace expires -> Outside (gain 0)
    for (int i = 0; i < 10; i++) g = c.Compute(0.9, 0.9, Dt);
    Check(Math.Abs(g) < Eps, "gain reaches 0 (Outside) after WINDOW_MS", $"gain={g}");
}

// ── 3) Re-entry from Outside -> Entering -> Inside ──
{
    Console.WriteLine("3) Re-enter from Outside (u=0.5, v=0.0) after being cut:");
    var c = new CornerAttenuation();
    double g = 1.0;
    for (int i = 0; i < 30; i++) g = c.Compute(0.9, 0.9, Dt); // drive to Outside
    Check(Math.Abs(g) < Eps, "precondition: at Outside (gain 0)", $"gain={g}");
    g = c.Compute(0.5, 0.0, Dt); // transition frame: cut persists one frame
    Check(Math.Abs(g) < Eps, "re-entry transition frame keeps cut (gain 0)", $"gain={g}");
    g = c.Compute(0.5, 0.0, Dt); // first Entering-update frame: starts rising
    Check(g > 0.0 && g < 1.0, "gain smooths up from 0 after re-entry", $"gain={g}");
    for (int i = 0; i < 80; i++) g = c.Compute(0.5, 0.0, Dt); // converge
    Check(Math.Abs(g - 1.0) < 1e-3, "gain converges back to 1.0 (Inside)", $"gain={g}");
}

// ── 4) Interrupt Entering with re-leave -> gain carries over ──
{
    Console.WriteLine("4) Interrupt Entering by leaving again:");
    var c = new CornerAttenuation();
    double g = 1.0;
    for (int i = 0; i < 30; i++) g = c.Compute(0.9, 0.9, Dt); // Outside
    for (int i = 0; i < 10; i++) g = c.Compute(0.5, 0.0, Dt); // Entering, gain rising
    double before = g;
    Check(before > 0.0 && before < 1.0, "mid-Entering gain in (0,1)", $"gain={before}");
    g = c.Compute(0.9, 0.9, Dt); // interrupt -> Leacing, gain carries over
    Check(g >= before - 1e-9, "gain carries over on interrupt (no reset to 0)", $"gain={g}, before={before}");
}

// ── 5) Re-enter during Leaving (within window) ──
{
    Console.WriteLine("5) Re-enter during Leaving window (u=0.5 briefly then in):");
    var c = new CornerAttenuation();
    double g = 1.0;
    g = c.Compute(0.9, 0.9, Dt);      // -> Leaving, gain 1.0
    g = c.Compute(0.9, 0.9, Dt);      // dip slightly
    double dipped = g;
    g = c.Compute(0.5, 0.0, Dt);      // re-enter -> Entering from dipped value
    Check(g >= dipped - 1e-9, "re-entry during window keeps dipped gain (no snap to 0)", $"gain={g}, dipped={dipped}");
    for (int i = 0; i < 80; i++) g = c.Compute(0.5, 0.0, Dt);
    Check(Math.Abs(g - 1.0) < 1e-3, "recovers to 1.0 after re-entry", $"gain={g}");
}

// ── 6) dt clamp safety: huge dt should not produce NaN/negative ──
{
    Console.WriteLine("6) Huge dt clamp (e.g. after stall):");
    var c = new CornerAttenuation();
    double g = c.Compute(0.9, 0.9, 100000.0); // far beyond WindowMs
    Check(g >= 0.0 && g <= 1.0 && !double.IsNaN(g), "gain stays in [0,1], no NaN", $"gain={g}");
}

Console.WriteLine();
Console.WriteLine("═══ SUMMARY ═══");
Console.WriteLine($"Corner attenuation tests: {pass} passed, {fail} failed");
Console.WriteLine(fail == 0 ? "Overall: ALL PASS" : "Overall: FAILURES DETECTED");

return fail == 0 ? 0 : 1;
