# TapOS

![Build and test](https://github.com/vbpy0304/SLAPSOUND/actions/workflows/build.yml/badge.svg)

TapOS is a lightweight Windows 11 tray utility that plays a random local WAV
sound when it recognizes a short four-contact gesture. It is designed to
remain unobtrusive: it starts in the notification area, uses preloaded audio
for low-latency playback, and can be enabled or paused from the tray.

> **Compatibility notice:** Four-finger touchpad input is hardware and driver
> dependent. TapOS uses documented Windows Raw Input/HID APIs and never
> installs a kernel driver or fabricates input events. If Windows does not
> expose a usable Precision Touchpad HID collection, the application reports
> that limitation instead of claiming the gesture works.

## Features

- Background Windows tray application with single-instance protection.
- Enable/disable from the tray or with `Win+Alt+M` when that hotkey is available.
- Random preloaded PCM WAV playback with consecutive-repeat avoidance.
- Configurable sound folder and gesture duration.
- Optional touchscreen overlay mode for four simultaneous touchscreen contacts.
- Diagnostics for hotkey registration, Raw Input, HID discovery, and touch
  message delivery.
- No network service, telemetry, kernel driver, or administrator access required.

## Requirements

- Windows 11.
- .NET 8 SDK to build from source.
- A Windows Precision Touchpad with a driver that exposes compatible Raw Input
  data for the physical-touchpad provider, or a touchscreen for overlay mode.
- PCM `.wav` files in the configured sound directory.

## Quick start

```powershell
git clone https://github.com/vbpy0304/SLAPSOUND.git
cd SLAPSOUND
dotnet run --project .\SLAPSOUND.csproj
```

TapOS starts hidden in the notification area. Open Settings from the tray,
confirm the sound directory, and use **Test sound** before testing hardware
input. The default sound directory is the repository's `sounds` folder.

Build a release binary with:

```powershell
dotnet publish .\SLAPSOUND.csproj -c Release -r win-x64 --self-contained false
```

The published application is written to
`bin\Release\net8.0-windows\win-x64\publish`.

## Testing

```powershell
dotnet build .\SLAPSOUND.csproj
dotnet test .\tests\TapOSTests\TapOSTests.csproj --no-restore
```

The automated tests cover the gesture recognizer and sound-library behavior.
Physical touchpad and touchscreen behavior must be tested on a Windows device.

## Runtime behavior

- The app starts hidden with a tray icon and prevents duplicate instances.
- `Win+Alt+M` toggles enabled/paused state while TapOS runs in the background.
- Tray actions open settings, toggle state, test sound, reload sounds, and exit.
- If another application owns `Win+Alt+M` (Windows error 1409), use the tray
  toggle or the Settings **Enable TapOS now** button instead.
- Settings stores user preferences in `%LOCALAPPDATA%\TapOS\settings.json`.
- Diagnostics show hotkey, Raw Input, HID, and touchscreen message state.

## Input providers and limitations

### Physical touchpad

TapOS uses the documented Raw Input/HID path. It registers for the Precision
Touchpad top-level collection (HID usage page `0x0D`, usage `0x05`), receives
`WM_INPUT`, obtains HID preparsed data, and reads the contact-count usage
(`0x0D/0x54`). A four-contact session that releases within the configured
maximum duration (default 500 ms) triggers playback.

This provider is hardware and driver dependent. The device must expose a
Windows Precision Touchpad HID collection and reports must be visible to Raw
Input. OEM firmware, non-PTP touchpads, virtualization, remote desktop,
privacy/power state, or future driver changes can prevent reports. TapOS
reports registration and device availability explicitly and never synthesizes
input or installs a kernel driver.

### Touchscreen overlay

The optional fullscreen overlay uses documented `WM_TOUCH` and `WM_POINTER`
messages to recognize four simultaneous **touchscreen** contacts. It is not a
touchpad solution. When active, the overlay is hit-testable and may intercept
mouse/touch interaction with applications underneath; pause it before normal
touchscreen use.

## Troubleshooting

1. Add valid PCM `.wav` files to the configured sound directory and use
   **Test sound** before testing hardware.
2. For touchscreen mode, enable the overlay checkbox, save, activate TapOS,
   touch the screen once, and confirm the WM_TOUCH/pointer counters increase.
3. For touchpad mode, activate TapOS and move fingers, then inspect the Raw
   Input and parsed-report counters in Settings.
4. If counters remain at zero, Windows or the device driver is not exposing the
   required input collection to TapOS. This is a platform/device limitation,
   not an audio-library failure.
