# TapOS

TapOS is a Windows 11 tray utility that loads `.wav` files and provides a global enable/disable hotkey (`Win+Alt+M`). It starts hidden, stays in the notification area, and exposes settings, sound testing, reload, pause/resume, and clear status/error messages.

## Important hardware/API limitation

TapOS now uses the documented Raw Input/HID path: it registers the hidden WPF window for the Precision Touchpad top-level collection (HID usage page `0x0D`, usage `0x05`), receives `WM_INPUT`, obtains HID preparsed data, and reads the mandatory Precision Touchpad contact-count usage (`0x0D/0x54`). A four-contact session that releases within the configured maximum duration (default 500 ms) triggers the audio path.

This is real device input, but it is not guaranteed on every touchpad. The device must expose a Windows Precision Touchpad HID collection and reports must be visible to Raw Input. OEM firmware, non-PTP touchpads, virtualization, remote desktop, device privacy/power state, or future driver changes can prevent reports. The UI reports registration/device availability explicitly. TapOS does **not** synthesize events or install a kernel driver. `WM_POINTER` and `WM_GESTURE` remain unrelated to the background raw HID path.

## Build and run

From this folder in a Windows PowerShell:

```powershell
dotnet build .\SLAPSOUND.csproj
dotnet run --project .\SLAPSOUND.csproj
```

The first launch creates settings under `%LOCALAPPDATA%\TapOS\settings.json`. Put `.wav` files in the configured sound folder (the default is the repository's `sounds` folder). Sounds are preloaded on reload and consecutive repeats are avoided whenever more than one sound is available.

## Behavior

- The app starts hidden with a tray icon.
- `Win+Alt+M` toggles enabled/paused state while the app is in the background.
- Tray actions open settings, toggle state, test sound, reload sounds, and exit.
- Hotkey registration failures and missing/unreadable sound folders are shown explicitly.
- If `Win+Alt+M` is unavailable because another application owns it (Windows error 1409), use the tray `Enable / disable` action or the Settings `Enable TapOS now` button; the hotkey is optional for activation.
- A named mutex prevents duplicate instances.
- Settings status shows Raw Input message/parsed-report counters and screen-pointer message/active-contact counters for troubleshooting.

## Optional screen-touch overlay

Settings includes an opt-in fullscreen transparent overlay that uses `WM_POINTER` to recognize four simultaneous **screen touchscreen** contacts and trigger the same audio path. It is not a touchpad solution and does not inspect Precision Touchpad HID reports.

When enabled and the app is active, the overlay covers the virtual desktop and is topmost. Because a window must receive the pointer messages, it is hit-testable and can intercept mouse/touch interaction with applications underneath. This tradeoff is intentional and is shown as a warning in the UI. Leave it disabled for normal desktop interaction. `Win+Alt+M` starts and stops the overlay together with the rest of TapOS.

## Input troubleshooting

1. Open Settings and enable the screen-touch overlay checkbox, then click **Save**.
2. Press `Win+Alt+M` once. The status must change to `Enabled`, and the screen-touch status must say `active fullscreen overlay`.
3. Touch the touchscreen once and reopen Settings. `Pointer messages` should be greater than zero. Four contacts must be placed and released within the configured duration; the sound is played on the final release.
4. For a touchpad, press `Win+Alt+M` and reopen Settings after moving fingers. `Raw Input messages` should increase and `parsed reports` should increase. If they remain zero, the device/driver is not exposing the PTP HID collection to this process; the status text will show whether registration or device discovery failed.

On the development machine used for validation, Raw Input messages were observed, but parsed reports remained zero and device enumeration did not find usage page `0x0D`, usage `0x05`. That is evidence of non-PTP/vendor-filtered HID input on that machine, not evidence that four-finger touchpad detection is working.
