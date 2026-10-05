# Headset Monitor — design and implementation plan

**Date:** 28 September 2026  
**Target:** Windows 10 x64; also test Windows 11 if available  
**Initial devices:** HyperX Cloud II Wireless (Kingston or HP receiver) and Logitech G933 Artemis Spectrum  
**Provisional device:** HyperX Cloud III S Wireless (`03f0:06be`), using direct HID and pending hardware verification
**Later device:** Original Plantronics RIG 800HD, subject to protocol research

## 1. Goal and scope

Build a small Windows notification-area application that shows the battery status of the two supported headsets from one place. A user can leave both USB receivers plugged in, see a row for each headset, and choose which headset's battery level appears on the tray icon. The application stays responsive if a receiver is removed, a headset is powered off, or battery information is temporarily unavailable.

V1 reads only battery and charging status where the underlying device supplies it. HyperX battery queries use direct HID; headset settings, audio routing, and the RIG 800HD protocol remain out of scope. Avoid claiming an exact percentage when a headset or backend only provides coarse levels.

**Existing alternative:** [aarol/headset-battery-indicator](https://github.com/aarol/headset-battery-indicator) already supplies a Windows tray indicator using HeadsetControl. Try it if the only goal is to use a battery indicator immediately. The plan below is for a C# app that the owner can extend and use for the later RIG work. Review the existing app's UX and behavior without copying code unless its GPLv3 license is suitable for the new project.

## 2. Verified starting point and assumptions

HeadsetControl supplies Logitech G933 battery data through its documented JSON CLI. HyperX Cloud II and Cloud III S use direct HID queries derived from auto94's MIT-licensed monitor: HP Cloud II `03f0:018b`/`03f0:0696`, Kingston Cloud II `0951:1718`, and HP Cloud III S `03f0:06be`. HidSharp provides managed HID enumeration and report I/O; the Kingston receiver additionally needs the Windows `HidD_GetInputReport` handshake used by the reference implementation.

The exact USB IDs and firmware of the owner's devices have **not** been confirmed. The earlier `047f:c043` identification for the RIG 800HD is a lead, not a verified ID for this owner's receiver. The RIG is deliberately not counted as supported until real hardware demonstrates a working battery read.

Windows 10 is the requested runtime target. Microsoft's support statement for modern .NET on Windows 10 now covers certain LTSC/Enterprise editions, so test the published binary on the owner's actual Windows 10 edition/build; do not treat successful compilation as proof of compatibility.

## 3. Proposed implementation

**UI:** C# WinForms, `net10.0-windows`, `NotifyIcon` and context menu; no main window required. Publish self-contained for `win-x64` once it works on the target machine. If .NET 10 fails on the owner's Windows 10 build, choose a compatible runtime after a real compatibility test and document that decision.

**Backend:** Use a composite source. `HyperXDirectHidSource` owns supported HyperX models and sends their model-specific battery request directly to the correct HID collection. `HeadsetControlCliSource` owns G933 and future explicitly allowed non-HyperX models by executing `headsetcontrol -o json`, validating the JSON API major version, and filtering returned devices. A failure in either source is retained as a diagnostic without discarding successful readings from the other. Do not scrape human-readable CLI text.

Display the detected HeadsetControl application and API versions in diagnostics. A newer backend can be tested or substituted without rebuilding the tray application when its JSON API remains compatible, but releases must continue to pin and test an exact version. Report incompatible output clearly and never silently reinterpret it. Do not implement automatic backend downloads or replacement in V1. When distributing HeadsetControl, include its GPLv3 license, attribution, corresponding-source information and any other required notices; obtain licensing advice for the intended distribution model.

**Polling:** On startup and every 30 minutes, run one backend query off the UI thread. No overlapping runs; set a reasonable timeout (for example 5 seconds), kill a hung child process, then retry at the next interval. Refresh immediately when the user selects **Refresh**; periodic polling is the fallback for plug/unplug and power changes. Redirection of stdout/stderr must avoid deadlock; never show a blocking error dialog from the poll loop.

**Domain model (internal):**

```text
DeviceKey = (vendorId, productId, modelName) [use stable receiver path if available later]
BatterySnapshot = {
  deviceKey, displayName, levelPercent?, charging?, state,
  precision, observedAtUtc, diagnostic?
}
state = Available | Charging | Unavailable | Disconnected | Error
precision = Exact | Coarse | Unknown
```

`levelPercent` is nullable. Treat 0 as a genuine measured value only when the backend reports it as available. Treat -1, absent fields, out-of-range levels, battery unavailable and the powered-off receiver state as **unknown**, never as 0%. If the backend does not expose charging or precision confidently, leave them unknown. Do not interpolate between coarse steps. Keep an earlier reading separately for diagnostics but never display it as a current reading after an unsuccessful poll.

**Adapter boundary:**

```csharp
public interface IHeadsetBatterySource
{
    Task<IReadOnlyList<BatterySnapshot>> ReadAsync(CancellationToken cancellationToken);
}

public sealed class HyperXDirectHidSource : IHeadsetBatterySource { /* direct HID adapter */ }
public sealed class HeadsetControlCliSource : IHeadsetBatterySource { /* JSON adapter */ }
public sealed class CompositeHeadsetBatterySource : IHeadsetBatterySource { /* merges sources */ }
```

Use a small coordinator to schedule polls, reconcile the current set of supported devices and publish immutable snapshots to the WinForms UI thread. Device filtering belongs in the adapter/coordinator, not in the tray rendering code. The later RIG driver should be added upstream to HeadsetControl if practical; it can then flow through this adapter. If upstream integration is blocked, a separate `Rig800HdSource` can implement the same interface and its snapshots can be merged by the coordinator.

## 4. User experience
- One persistent tray icon represents the selected headset. When a current battery level is available, show a colour-coded percentage. Otherwise show a headset/status icon: grey `?` for off, disconnected or unavailable, and orange `!` for an error. If charging is reported, add a charging indicator where it remains legible.
- Default colour thresholds are green above 50%, yellow from 21–50%, and red at or below 20%. Store these as two ordered thresholds rather than three independent ranges: `0 <= critical < warning <= 100`. The settings UI labels them **Critical at or below** and **Warning at or below**.
- The tooltip contains the device-status rows from the right-click menu, not command items such as Refresh or Exit. Include headset names and states, and truncate safely to the Windows tooltip limit.
- Right-click menu contains **HyperX Cloud II Wireless — 72%**, **Logitech G933 — 45%**, or clear states such as **Headset off**, **Receiver unplugged**, **Reading unavailable**. Clicking a row selects that headset for the tray number; selection persists across restarts.
- **Refresh**, **Settings**, **About / diagnostics**, and **Exit** menu actions. Settings live in the user's local application data. Diagnostics show app/backend version, recognized IDs, last poll time, and redacted errors, with a copy button.
- If both receivers are present, both rows remain visible. If none is present, the icon stays available with `?` and the menu explains why. Restarting Explorer should not permanently strand the app; validate tray icon recreation during manual testing.
- Low-battery notifications are a follow-up increment: opt-in, use the configurable critical threshold, trigger only on a transition from above the threshold to at/below it, and suppress repeated alerts until charge/recovery. Unknown status never triggers an alert.
- A **Settings** action opens a small settings window. The user can change the polling interval (default 30 minutes; valid range 1 minute to 24 hours), warning/critical thresholds, and an opt-in **Start automatically when I sign in** checkbox. Validate values before saving, offer reset-to-defaults, persist changes in local application data, and apply them immediately without starting an overlapping poll. Autostart is registered per user and disabling it removes the application's startup entry.
  
## 5. Milestones and acceptance criteria

### M0 — hardware and backend spike

On the owner's Windows 10 PC, record Windows edition/build, all receiver IDs, and the HeadsetControl version. Test each HyperX receiver through the direct HID source with the headset powered on and off. Save **redacted** HeadsetControl JSON for the G933. Then test all receivers together and with NGENUITY/G HUB both running and closed. **Gate:** record what is verified versus still untested.

### M1 — minimum working tray app

Create a C# solution containing the WinForms tray process, JSON adapter and coordinator. Discover and show both devices, use the default colour thresholds for a dynamic percentage/status icon, switch the selected headset, support manual refresh and exit, and persist selection. Handle process timeout, invalid JSON and unplug events without freezing the UI. **Done when:** both physical headsets can be shown in the menu simultaneously and the icon follows the selected one on the actual Windows 10 machine.

### M2 — reliability and distribution

Add the validated settings window, opt-in start at sign-in, modest diagnostics, resilient periodic refresh and an x64 self-contained publish. Build a per-user Inno Setup installer that requires no elevation and contains the exact tested HeadsetControl binary plus its required licence and corresponding-source information. The packaging build must fail when any required dependency material is absent. **Done when:** a clean Windows 10 user profile can install, upgrade, run and uninstall it; settings persist and take effect; unplug/replug and power transitions are reflected correctly; and it recovers after sign-out/sign-in and Explorer restart.

### M3 — later Plantronics RIG 800HD research

Only after M2: verify this receiver's VID/PID, enumerate its HID collections and dump its report descriptor. Capture relevant traffic while off/on and during the spoken battery announcement. Establish that the receiver actually exposes usable battery data; document report IDs, request/response lengths, value encoding, unavailable states and test captures. Add a battery-only driver to HeadsetControl and propose an upstream PR if feasible. The tray app then needs at most a new supported-device mapping; otherwise implement a separate source behind `IHeadsetBatterySource`. **Done when:** a repeatable battery reading is independently confirmed on real RIG hardware. There is no commitment to claim RIG support if its receiver does not expose battery state.

## 6. Verification checklist

| Case | Expected result |
| --- | --- |
Neither receiver plugged in | App remains responsive; no fabricated percentage |
HyperX only; G933 only; both | Correct devices and distinct readings; selection works |
Headset off, receiver still plugged in | Current level becomes unknown/offline when backend says so |
Unplug and reconnect during a poll | No crash, no stale percentage presented as current |
Backend missing, exits nonzero, emits malformed JSON, or hangs | Clear diagnostic; UI stays responsive; later polling recovers |
Level reported as 0, -1, null, or >100 | Only valid available 0–100 accepted; other values unavailable |
Charging with unavailable percentage | Charging shown only if reported; no invented number |
Levels at 0, critical, critical+1, warning and warning+1 | Correct red/yellow/green boundary colour |
Invalid thresholds or polling interval | Cannot be saved; existing settings remain active |
Polling interval changed during an active poll | No overlapping backend process; new interval applies afterward |
Explorer restart, Windows sign-in, clean user profile | Tray icon returns; opt-in startup behavior preserved |
Hardware readings compared with vendor utility or known discharge | Plausible and appropriately coarse; document discrepancies |

Automate JSON parsing, state mapping and polling/timeout behavior with fixture payloads. Perform the hardware cases manually on Windows 10; CI or Linux tests cannot validate the receivers. Avoid tests that assert a particular percentage independent of battery discharge.

## 7. Risks and constraints

- The direct HyperX protocol and known IDs come from another implementation, but the owner's firmware and receiver revisions may differ. Finish M0 before claiming hardware acceptance.
- A dongle can remain connected while its headset is off. Backend status takes precedence over the presence of a USB receiver.
- Some devices only expose coarse battery levels; the tray must reflect reported precision rather than imply one-percent accuracy.
- HID access can conflict with other software. Test with Logitech G HUB and HyperX NGENUITY running and closed; document reproducible conflicts rather than blindly killing either process.
- HeadsetControl and the existing Rust tray app are GPLv3. If distributing a package that contains HeadsetControl or borrowing GPL code, plan license notices and source availability accordingly. Have licensing reviewed for any different distribution model; do not assume a subprocess boundary settles licensing questions.
- Initial design handles one receiver per model. If the user later owns two identical receivers, extend `DeviceKey` using a stable device instance path if the chosen backend exposes one.

## 8. Suggested repository structure

```text
HeadsetMonitor.slnx
src/HeadsetMonitor/          # WinForms tray, coordinator, settings
src/HeadsetMonitor.Core/     # Snapshot, status and parsing contracts
src/HeadsetMonitor.Backends/ # Direct HyperX HID and HeadsetControl adapters
tests/HeadsetMonitor.Tests/  # JSON and error-state fixtures
docs/hardware-notes.md              # Actual IDs, backend versions, observations
docs/rig800hd-research.md           # Later only; evidence and protocol findings
LICENSES/                           # Notices and applicable third-party licenses
```

## 9. Copy-paste prompt for Codex

> Implement milestones M0 through M2 in `Headset_Monitor_Design_and_Implementation.md` as a Windows 10 tray application. First inspect the repository and report the exact HeadsetControl backend version and actual connected-device evidence available. Build the C# WinForms `net10.0-windows` UI, JSON CLI adapter, scheduler and tests specified in the plan. Do not invent headset percentages, support IDs, charging flags or successful hardware test results. If there is no Windows machine or hardware available in your environment, finish the implementation and automated tests, provide exact Windows 10 commands and a short hardware verification checklist, and label hardware acceptance as pending. Make a distributable self-contained x64 build when a Windows build environment is available. Keep the RIG 800HD as a later milestone; do not send speculative HID reports to it. Preserve third-party license notices. At the end, report changed files, test results, hardware evidence, unresolved issues and the next concrete action.

## Primary references

- [HeadsetControl supported devices, Windows download, output formats and license](https://github.com/Sapd/HeadsetControl/blob/master/README.md)
- [HeadsetControl documented JSON output, C/C++ API and C API](https://github.com/Sapd/HeadsetControl/blob/master/docs/LIBRARY_USAGE.md)
- [HeadsetControl releases and battery fixes](https://github.com/Sapd/HeadsetControl/releases)
- [auto94 HyperX direct-HID battery monitor](https://github.com/auto94/HyperX-Cloud-2-Battery-Monitor)
- [HidSharp package](https://www.nuget.org/packages/HidSharp)
- [Existing Windows tray implementation and GPLv3 license](https://github.com/aarol/headset-battery-indicator)
- [Microsoft .NET Windows support matrix](https://learn.microsoft.com/en-us/dotnet/core/install/windows)
