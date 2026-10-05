# Building the Windows installer

## Prerequisites

- Windows x64 with the .NET 10 SDK
- Inno Setup 6 or 7 (`ISCC.exe` on `PATH`, in its system-wide or per-user default installation directory, or passed explicitly)
- The verified HeadsetControl inputs described in [`packaging/headsetcontrol/README.md`](../packaging/headsetcontrol/README.md)

## Build

From the repository root in PowerShell:

```powershell
.\installer\build-installer.ps1 -AppVersion 0.1.0
```

The script automatically detects per-user Inno Setup 7 installations such as `%LocalAppData%\Programs\Inno Setup 7\ISCC.exe`. For a custom installation location, pass the compiler explicitly:

```powershell
.\installer\build-installer.ps1 -InnoCompiler 'C:\path\to\ISCC.exe'
```

The script publishes a self-contained, single-file `win-x64` application, validates the required HeadsetControl executable, and creates:

```text
artifacts\installer\HeadsetMonitor-0.1.0-win-x64-setup.exe
```

The installer is per-user, does not require elevation, optionally creates a Start Menu shortcut, and offers to launch the application when setup finishes. The application's **Start automatically when I sign in** setting remains opt-in. Uninstall removes that startup entry but intentionally preserves the user's settings under `%LocalAppData%\HeadsetMonitor`.

Before distribution, test install, upgrade, launch, autostart, uninstall, Windows Defender/SmartScreen behavior, and both physical headsets on the target Windows 10 build. Code signing is strongly recommended for public releases.
