# GazeStick

Turn your webcam into a virtual gamepad right stick using eye tracking.

GazeStick captures gaze data from the [Beam Eye Tracker](https://beam.eyeware.tech/) SDK and maps it to the right thumbstick (RX/RY) of a virtual Xbox 360 controller via [ViGEmBus](https://vigem.org/). No dedicated eye tracking hardware required — just a webcam.

## Prerequisites

- **Windows 10 or 11** (x64)
- **ViGEmBus driver** — [Download](https://github.com/nefarius/ViGEmBus/releases/latest)
- **Beam Eye Tracker app** — [Download](https://beam.eyeware.tech/) (free tier works)
- **Beam SDK native DLL** (`beam_eye_tracker_client.dll`) — included in the SDK package from [docs.beam.eyeware.tech](https://docs.beam.eyeware.tech/)

## Setup

1. Install [ViGEmBus](https://github.com/nefarius/ViGEmBus/releases/latest) and restart if prompted
2. Install and run the [Beam Eye Tracker](https://beam.eyeware.tech/) app, sign in, and activate **Gaming Extensions**
3. Download the Beam SDK and copy `beam-sdk/bin/win64/beam_eye_tracker_client.dll` to a convenient location
4. Build GazeStick (see below) or download a release

### SDK DLL placement

Place `beam_eye_tracker_client.dll` in one of these locations:
- `beam-sdk/bin/win64/beam_eye_tracker_client.dll` (project root, auto-copied on build)
- Same directory as `GazeStick.exe`

## Build

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

Output: `bin/Release/net8.0-windows/win-x64/GazeStick.exe`

## Usage

1. Launch **GazeStick** (appears in system tray)
2. Make sure **Beam Eye Tracker** is running and tracking
3. Look at the center of your screen to keep the stick neutral
4. Look toward edges to move the stick

| Action | Input |
|--------|-------|
| Toggle ON/OFF | Left-click tray icon, or press `F9` |
| Open settings | Right-click tray icon |
| Adjust deadzone/sensitivity/smoothing | Drag value or press +/- in the popup panel |
| Toggle Y-invert | Click the Y badge in the popup panel |
| Change toggle hotkey | Click the hotkey badge, then press new key |
| Quit | Click "Exit" in the popup panel |

## Configuration

Settings are saved to `%AppData%\GazeStick\settings.json`.

| Key | Default | Description |
|-----|---------|-------------|
| `deadzone` | 0.10 | Circular deadzone radius (0.00–0.50) |
| `sensitivity` | 1.0 | Stick output multiplier (0.1–5.0) |
| `smoothing` | 0.30 | EMA smoothing factor (0.0–0.9) |
| `invertY` | false | Invert Y-axis output |
| `toggleHotkey` | "F9" | Global toggle hotkey |
| `padSlot` | auto | Virtual pad slot number (auto-assigned) |
| `startWithWindows` | true | Auto-start with Windows |
| `startActive` | true | Start with tracking active |

## How it works

```
[Beam Eye Tracker App] → (local socket) → [GazeStick]
                                               ├─ TrackingService  — polls Beam SDK at ~60fps
                                               ├─ StickMapper      — deadzone → sensitivity → smoothing → output
                                               ├─ VirtualPad       — ViGEm Xbox 360 controller (right stick)
                                               └─ TrayApp          — system tray icon + popup settings panel
```

## Architecture

- **Language:** C# (.NET 8, WinForms)
- **Eye tracking:** [Beam Eye Tracker SDK](https://docs.beam.eyeware.tech/) (native DLL, P/Invoke)
- **Virtual controller:** [ViGEmBus](https://github.com/nefarius/ViGEmBus) via [Nefarius.ViGEm.Client](https://www.nuget.org/packages/Nefarius.ViGEm.Client)
- **UI:** System tray with popup settings panel (no main window)

## Designed for Combination

GazeStick provides only the right stick output. On its own this is limited, but as an accessibility tool it is designed to be paired with other input devices and controller remapping software.

Applications like [reWASD](https://www.rewasd.com/) can merge multiple virtual and physical controllers into one unified controller, allowing you to combine GazeStick's right stick output with a physical controller's left stick, keyboard, mouse, or other inputs. This is the standard pattern: each tool handles one aspect of input, and a remapping layer ties them together.

## Third-Party Licenses

This application uses the Beam Eye Tracker SDK, which includes third-party components with the following licenses:

- [ZeroMQ (libzmq)](https://github.com/zeromq/libzmq) — Mozilla Public License 2.0
- [libsodium](https://github.com/jedisct1/libsodium) — ISC License
- [Protocol Buffers](https://github.com/protocolbuffers/protobuf) — BSD 3-Clause License
- [Eigen](https://gitlab.com/libeigen/eigen) — Mozilla Public License 2.0
- [cppzmq](https://github.com/zeromq/cppzmq) — MIT License
- [utfcpp](https://github.com/nemtrif/utfcpp) — Boost Software License 1.0
- [pybind11](https://github.com/pybind/pybind11) — BSD 3-Clause License

See [`beam-sdk/THIRD_PARTY_LICENSES.md`](beam-sdk/THIRD_PARTY_LICENSES.md) for the full license texts.

## Disclaimers

**Non-Medical Device:** This software and the underlying Beam Eye Tracker SDK are not medical devices. They are not intended, nor should they be used, to replace professional medical advice, diagnosis, or treatment.

**High-Risk Use Prohibition:** This software must not be used in high-risk environments or safety-critical applications where any software malfunction or interruption could lead to personal injury, loss of life, or physical/environmental damage.

**Data Privacy:** Eye-tracking / gaze data is processed entirely locally on your machine and is used only for controller mapping. No gaze data is logged, recorded, or transmitted to any remote server without your explicit consent.

## License

MIT
