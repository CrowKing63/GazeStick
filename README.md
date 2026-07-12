# GazeStick

Turn your webcam into a virtual gamepad right stick using eye tracking.

GazeStick receives local gaze data from the [Beam Eye Tracker](https://beam.eyeware.tech/) SDK and maps it to the right thumbstick of a virtual Xbox 360 or DualShock 4 controller through [ViGEmBus](https://vigem.org/). No dedicated eye-tracking hardware is required—just a webcam.

## Prerequisites

- Windows 10 or 11 (x64)
- [ViGEmBus](https://github.com/nefarius/ViGEmBus/releases/latest)
- The official [Beam Eye Tracker app](https://beam.eyeware.tech/) installed, running, and activated with a valid subscription/license
- `beam_eye_tracker_client.dll` beside the executable; the build and release pipeline copy this DLL automatically

## Output modes

| Mode | Default | Notes |
|---|---:|---|
| Xbox 360 | Yes | Broad game compatibility; consumes one of Windows' four XInput controller slots. |
| DualShock 4 | No | Does not consume an XInput slot; game or mapping-layer support for DS4 input may be required. |

Change the output mode from the tray popup. GazeStick immediately disconnects the old virtual controller, then connects the selected one in a neutral state.

## Setup and usage

1. Install ViGEmBus and restart if prompted.
2. Install, sign in to, and run Beam Eye Tracker. Activate Gaming Extensions.
3. Run GazeStick from the system tray.
4. Look at the screen center to keep the right stick neutral; look toward an edge to move it.

| Action | Input |
|---|---|
| Toggle tracking | Tray icon or `F9` |
| Open settings | Click the tray icon |
| Change output mode | Select Xbox 360 or DualShock 4 in the popup |
| Adjust tracking | Use the Deadzone, Sensitivity, Smoothing, and Curve controls |
| Close settings | Press `Esc` or click anywhere outside the popup |

## Configuration

Settings are saved to `%AppData%\GazeStick\settings.json`.

| Key | Default | Description |
|---|---:|---|
| `deadzone` | 0.10 | Circular neutral radius (0.00–0.50). |
| `sensitivity` | 2.0 | Stick output multiplier (0.1–5.0). |
| `smoothing` | 0.30 | EMA smoothing factor (0.0–0.9). |
| `outputType` | `Xbox360` | Virtual-controller output mode. |
| `invertY` | false | Invert vertical camera output. |
| `toggleHotkey` | `F9` | Global tracking-toggle hotkey. |

## Designed for combination

GazeStick intentionally provides only right-stick output. Pair it with your preferred controller-mapping or device-combination software when your setup needs to combine gaze, physical controllers, keyboard, mouse, or other inputs. Compatibility depends on the selected mapping software, virtual-controller mode, and game.

## Build

```powershell
scripts/fetch-sdk.ps1
dotnet publish -c Release -r win-x64 --self-contained false
```

The Beam DLL is copied from `lib/beam_eye_tracker_client.dll` to the executable output directory. Use `package.ps1` for a portable package or `scripts/build-installer.ps1` for an installer.

## Privacy and safety

Gaze data is processed locally for controller mapping. GazeStick does not log, record, or transmit gaze data to an unauthorized remote service.

**Non-Medical Device Disclaimer:** This software and the underlying Beam Eye Tracker SDK are not medical devices. They are not intended, nor should they be used, to replace professional medical advice, diagnosis, or treatment.

**High-Risk Use Prohibition:** This software must not be used in high-risk environments or safety-critical applications where any software malfunction or interruption could lead to personal injury, loss of life, or physical/environmental damage.

## Changelog

### v1.2.0

- Added immediate Xbox 360 / DualShock 4 output selection.
- Changed the default sensitivity to 2.0.
- Redesigned the tray popup for visibility, readability, and outside-click dismissal.
- Updated combination-tool guidance to be vendor neutral.

## License

MIT
