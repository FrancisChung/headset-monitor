# Headset Monitor

A Windows notification-area application for monitoring the HyperX Cloud II Wireless, HyperX Cloud III S Wireless, and Logitech G933.

HyperX Cloud II and Cloud III S battery levels are read directly from their USB receivers using the protocol documented by [auto94/HyperX-Cloud-2-Battery-Monitor](https://github.com/auto94/HyperX-Cloud-2-Battery-Monitor). Logitech G933 remains handled by HeadsetControl. The sources are polled independently, so one backend failing does not discard readings returned by the other.

Known HyperX receiver identities are HP Cloud II `03f0:018b` and `03f0:0696`, Kingston Cloud II `0951:1718`, and HP Cloud III S `03f0:06be`. These paths still require verification on the owner's physical hardware. Charging state is left unknown because the adopted battery queries do not establish it.

Implementation is in progress. The application expects a pinned Windows build of `headsetcontrol.exe` beside `HeadsetBatteryMonitor.exe` for non-HyperX devices; an advanced custom path will be supported for compatibility testing. HeadsetControl is not currently checked into this repository.

The platform-neutral parser and settings tests can be run with:

```shell
dotnet test tests/HeadsetBatteryMonitor.Tests/HeadsetBatteryMonitor.Tests.csproj -m:1
```

The WinForms shell targets `net10.0-windows` and must be hardware-tested on the intended Windows 10 machine. See [the design and implementation plan](Docs/Headset_Battery_Monitor_Design_and_Implementation.md).

Windows installer scaffolding is documented in [installer/README.md](installer/README.md). A release installer requires a separately supplied and verified Windows x64 `headsetcontrol.exe` plus its required GPL materials.

Third-party attribution and licensing notes are in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
