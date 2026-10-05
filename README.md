# Headset Monitor

A Windows notification-area application for monitoring the HyperX Cloud II Wireless, HyperX Cloud III S Wireless, and Logitech G933.

HyperX Cloud II and Cloud III S battery levels are read directly from their USB receivers using the protocol documented by [auto94/HyperX-Cloud-2-Battery-Monitor](https://github.com/auto94/HyperX-Cloud-2-Battery-Monitor). Logitech G933 remains handled by HeadsetControl. The sources are polled independently, so one backend failing does not discard readings returned by the other.

Known HyperX receiver identities are HP Cloud II `03f0:018b` and `03f0:0696`, Kingston Cloud II `0951:1718`, and HP Cloud III S `03f0:06be`. These paths still require verification on the owner's physical hardware. Charging state is left unknown because the adopted battery queries do not establish it.

Implementation is in progress. The application expects a pinned Windows build of `headsetcontrol.exe` beside `HeadsetMonitor.exe` for non-HyperX devices; an advanced custom path will be supported for compatibility testing. HeadsetControl is not currently checked into this repository.

## Building on Windows

Install the **.NET 10 SDK** (installing only the .NET runtime is not sufficient), then confirm that a `10.0.xxx` SDK is available:

```powershell
dotnet --list-sdks
```

From the repository root, build the solution with:

```powershell
dotnet build .\HeadsetMonitor.slnx --configuration Release -m:1
```

The solution uses the newer `.slnx` format and targets `net10.0`/`net10.0-windows`. If Visual Studio cannot open the solution, update Visual Studio to a version that supports the installed .NET 10 SDK or run the command-line build above. Keep the `-m:1` option if MSBuild otherwise exits without reporting a useful error.

The installer cannot be built from a fresh clone without the HeadsetControl executable. Before running `installer\build-installer.ps1`, install Inno Setup 6 and provide this deliberately untracked file:

```text
packaging\headsetcontrol\headsetcontrol.exe
```

See [the installer instructions](installer/README.md) and [the HeadsetControl packaging instructions](packaging/headsetcontrol/README.md) for provenance, verification, and licensing requirements. A missing input causes the installer script to stop with `Required packaging input is missing`; that does not indicate a source-code compilation failure.

## Tests

The platform-neutral parser and settings tests can be run with:

```shell
dotnet test tests/HeadsetMonitor.Tests/HeadsetMonitor.Tests.csproj -m:1
```

The WinForms shell targets `net10.0-windows` and must be hardware-tested on the intended Windows 10 machine. See [the design and implementation plan](Docs/Headset_Monitor_Design_and_Implementation.md).

Windows installer scaffolding is documented in [installer/README.md](installer/README.md). A release installer requires a separately supplied and verified Windows x64 `headsetcontrol.exe`.

Third-party attribution and licensing notes are in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
