# Corner Attenuation Logic — Design (v1.1.2)

Pre-measurement baseline design. Parameters are conservative defaults pending
real-world tuning.

## 0. Terminology (to avoid confusion)

- **Cardinal points**: the four points reached by going from screen center up /
  down / left / right until the screen boundary. Coordinates:
  `(±halfWidth, 0)`, `(0, ±halfHeight)`. The ellipse passing through these four
  points is the reference baseline of this design.
- **Corner**: a physical screen corner (e.g. the top-right end). These points
  lie *outside* the ellipse above. This is the region the attenuation targets.
- In this document "ellipse" always means the ellipse through the four cardinal
  points, never a circle/ellipse through the corners.

## 1. Core idea

Using the virtual ellipse through the four cardinal edge-midpoints as a
reference: inside the ellipse gaze is handled 1:1. Outside the ellipse (corner
regions included) directional jitter (noise) is attenuated so corner UI can be
inspected stably.

## 2. Geometry (fixed mathematically, no measurement needed)

Normalized coordinates: `u = dx / halfWidth`, `v = dy / halfHeight`.

Ellipse membership: `u² + v² ≤ 1`.

The point where a diagonal direction (toward a screen corner) crosses the
ellipse boundary is always at `1/√2 ≈ 70.7%` of the full diagonal distance,
regardless of aspect ratio (16:9, 4:3, 21:9, ...). This is an algebraic result
because the corner direction aligns exactly with the u/v axes.

## 3. State machine

- **INSIDE** — `gain = 1.0`, raw 1:1 output, no smoothing.
  - On `u² + v² > 1` immediately → enter **LEAVING**.

- **LEAVING** (grace window, while a gentle attenuation starts):
  - Track elapsed `t` (ms) via delta-time accumulation.
  - `gain = 1.0 - (t / WINDOW_MS) * DIP_DEPTH` (floored at `1.0 - DIP_DEPTH`).
  - If during this state `u² + v² ≤ 1` → re-converge to 1.0 using the same
    exponential smoothing as ENTERING, starting from the current gain
    (no instant snap); enter **ENTERING**.
  - If `t ≥ WINDOW_MS` (grace expires with no re-entry) → switch to **OUTSIDE**.

- **OUTSIDE** (confirmed departure) — `gain = 0` (full cut).
  - On `u² + v² ≤ 1` → enter **ENTERING**.

- **ENTERING** (re-entry buffering) — exponential smoothing of gain `0 → 1`;
  target is refreshed to the latest raw vector each frame.
  - On reaching the convergence threshold or max time → switch to **INSIDE**.
  - If `u² + v² > 1` occurs mid-buffering → interrupt immediately and switch to
    **LEAVING**.

All transitions must be interruptible, and `gain` must always continue
naturally from its current value (never instant-reset to 0 or 1).

## 4. Baseline parameters (proposed pre-measurement values — conservative/safe)

| Parameter | Default | Rationale |
| --- | --- | --- |
| `WINDOW_MS` (LEAVING grace) | 130 ms | ~4 frames at 30 fps; short saccade-like returns usually end faster |
| `DIP_DEPTH` (max LEAVING dip) | 0.3 (1.0 → 0.7 only) | not fully killed, keeps re-entry dip shallow |
| `ENTERING` exp. smoothing τ | 80–100 ms | mostly converges within 2–3 frames at 30 fps |
| time handling | ms-based, delta-time accumulation (no frame counters) | identical feel across 30/60/90 fps |

## 5. Implementation requirements

- Investigate the relationship with the existing smoothing option first: confirm
  whether smoothing is applied at the raw gaze stage or the final stick-vector
  stage. This attenuation must be added as a **separate layer after the elliptical
  clamp and just before the final vector output**; if it overlaps the existing
  smoothing, double delay occurs, so stages must be clearly separated.
- Delta-time-based implementation is mandatory. Frame-counter timers are forbidden.
- Add a debug log hook: an optional per-frame log of `state, gain, t, u, v` so
  that after a few minutes of play, parameter tuning is possible purely by
  inspecting the log file.
- No game-specific configuration; the goal is a universal default. Do not create
  additional exposed settings beyond these four parameters (tune the baseline
  values in this document after measurement if needed).

## 6. Post-measurement tuning targets (not fixed arbitrarily yet)

- `WINDOW_MS` — actual chattering frequency / duration.
- `DIP_DEPTH` — whether the dip is perceptible during real play.
- `ENTERING τ` — whether it feels too sluggish or over-snappy.

## 7. Implementation notes (GazeStick)

- `CornerAttenuation` (Services/CornerAttenuation.cs) owns the state machine and
  all constants. `Compute(u, v, dt)` must be called **exactly once per frame**
  (it accumulates time) and its result reused; it must not be called twice per
  frame.
- `StickMapper.Map` computes `u = 2*(x-0.5)`, `v = 2*(y-0.5)` and `dt` from a
  `Stopwatch`, calls `Compute` once at the top, then applies the returned gain to
  the already-smoothed stick vector (after the existing smoothing stage, before
  `InvertY`).
- Debug log is CSV (`timestamp,state,gain,t,u,v`), written to
  `%TEMP%/GazeStick-corner.log`, gated by the `GAZESTICK_CORNER_DEBUG=1`
  environment variable. Session start overwrites the file; lines are buffered and
  flushed every ~30 frames; the file is restarted if it exceeds 5 MB.
