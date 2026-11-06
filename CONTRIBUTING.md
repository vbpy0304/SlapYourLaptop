# Contributing to TapOS

Thanks for your interest in TapOS. Contributions are welcome, especially
hardware compatibility reports, documented Windows API findings, and focused
bug fixes.

## Development setup

1. Install the .NET 8 SDK on Windows 11.
2. Clone the repository.
3. Build the application:

   ```powershell
   dotnet build .\SLAPSOUND.csproj
   ```

4. Run the tests:

   ```powershell
   dotnet test .\tests\TapOSTests\TapOSTests.csproj --no-restore
   ```

## Pull requests

- Keep changes focused and explain hardware-specific behavior clearly.
- Do not add undocumented drivers, kernel hooks, or synthetic input events.
- Include tests for changes that do not require physical hardware.
- Document any limitations observed on a specific laptop or touchpad.
- Do not commit build output, local settings, logs, or credentials.

## Hardware reports

Include the Windows version, laptop model, touchpad model if known, whether
the device is a Windows Precision Touchpad, and the TapOS diagnostics shown in
Settings. Do not include personal files or sensitive system information.
