# Features

## 1. Reference application inventory (analysis only)

The reference product exposes the following capability areas. This table records
what was observed in the decompiled project; none of its code, assets, or
binaries are carried into GOLEM TABLET.

| Area | Reference evidence | Behaviour |
|---|---|---|
| Samsung flashing | Odin client types, PIT handling, TAR utilities | Full Odin flash workflow |
| Qualcomm EDL | EDL library + flash-entry model | EDL partition read/write |
| Spreadtrum | SPRD protocol state machine, flash utilities | SPRD flash workflow |
| MediaTek | AT transport + vendor protocol | AT diagnostics and operations |
| ADB | ADB runner service | Shell, properties, packages, reboot (system/recovery/bootloader/EDL/download), logcat, screenshot, push/pull |
| Device modes | Mode detector + serial-port resolver | ADB / fastboot / EDL / Odin / COM detection |
| Drivers | Driver-fix service | USB driver check and repair |
| Remote assets | Remote asset store | Download of auxiliary assets |
| Updates / licence | Version gate + updater + licence server | Mandatory update, licence enforcement |
| Logging | Log models | Severity, timestamps |

## 2. Verification environment

All statuses below refer to **this** verification run, not to intent.

| Item | Value |
|---|---|
| OS | Windows 10 22H2 x64 |
| SDK | .NET SDK 8.0.427 |
| Build | `dotnet build Golem.Tablet.sln -c Release` — succeeded, 0 warnings, 0 errors |
| Tests | `dotnet test Golem.Tablet.sln` — 140 passed, 0 failed |
| App start / shutdown | Verified: window opened, all eleven pages rendered, closed cleanly |
| Published build | `dotnet publish -c Release -r win-x64 --self-contained` run from `artifacts/win-x64`; title `GOLEM TABLET`, exit code 0 |
| Application icon | Verified in the published build: gold GOLEM monogram in the window header; the executable carries a multi-size icon (16–256 px) derived from the project brand artwork, with a simplified monogram at 16–32 px, confirmed from the built binary's associated icon |
| Android hardware | **None attached.** No device-dependent feature could be run against real hardware. |
| `adb.exe` | **Not installed** on the test machine. Only the missing-tool path was exercised. |
| Serial ports | `COM1` present, but no tablet/modem attached. |

Status legend:

- **Implemented** — code exists and builds.
- **Tested (unit)** — covered by an automated unit/integration test.
- **Tested (live)** — executed in the running application in this environment.
- **Not tested** — implemented but could not be exercised here (no hardware/tool).
- **Blocked** — deliberately not implemented; no right or dependency to do it honestly.

## 3. Feature matrix

### A. Device Manager

| Feature | Status | Evidence / notes |
|---|---|---|
| Enumerate connected devices | Implemented · Tested (live) | WMI `Win32_PnPEntity` over `USB\`/`USBSTOR\` IDs; 10 devices enumerated here |
| Show connection status | Implemented · Tested (live) | Device status and `ConfigManagerErrorCode` shown in the detail pane |
| Show device information | Implemented · Tested (live) | Name, VID/PID, serial, driver service, PnP ID |
| Refresh / rescan | Implemented · Tested (live) | Async scan, exercised at startup and via Refresh; cancellable |
| Distinguish connection modes | Implemented · Tested (unit) | Pure classifier: ADB, fastboot, Odin download, Qualcomm EDL, MediaTek preloader, serial/AT, mass storage. Not tested against hardware of each family. |
| Clear errors when nothing is detected | Implemented · Tested (live) | Empty-state text; scan failures are logged with the reason |

### B. ADB Tools

| Feature | Status | Evidence / notes |
|---|---|---|
| Detect ADB availability | Implemented · Tested (live, failure path) | Configured path, then `PATH`; absent `adb.exe` produced an explicit error here |
| List devices with identifiers and states | Implemented · Tested (unit) | `adb devices -l` parser, including `offline` and `no permissions`; **not run against a live adb server** |
| Execute supported commands | Implemented · Not tested | Curated presets plus guarded `adb shell`; requires `adb.exe` and a device |
| Authorized reboot operations | Implemented · Not tested | system / recovery / bootloader / EDL / download, confirmation governed by Settings |
| Device info and diagnostics | Implemented · Not tested | `getprop`, Android ID, `df` |
| List third-party packages | Implemented · Tested (unit) | `adb shell pm list packages -3`; argument builder unit-tested, needs a device |
| Uninstall a package | Implemented · Tested (unit) | `pm uninstall --user 0 <package>` after name validation; arguments tested, no device to run against |
| Capture a logcat snapshot | Implemented · Tested (unit) | `adb shell logcat -d -t <lines>` (clamped 1–5000, default 400); shown in LAST OUTPUT |
| Capture a screenshot | Implemented · Tested (unit) | `screencap -p` on the device, pulled into a file the operator chooses, remote copy removed |
| Push a local file | Implemented · Tested (unit) | `adb push <local> <remote dir>`; remote directory editable, default `/sdcard/` |
| Pull a remote file | Implemented · Tested (unit) | `adb pull <remote file> <local path>`; a folder-only remote path is rejected |
| Install an APK | Implemented · Tested (unit) | `adb install -r <local .apk>`; the file is chosen on this computer, confirmation governed by Settings |
| Extract an installed APK | Implemented · Tested (unit) | `pm path <package>` parsed for the base APK, then pulled to a chosen file |
| Record the screen | Implemented · Tested (unit) | `screenrecord --time-limit 20`, pulled locally, device copy removed; command timeout extended past the recording length |
| Device profile | Implemented · Tested (unit) | `getprop` parsed into model, manufacturer, Android version/SDK, build fingerprint, security patch, bootloader, ABI |
| Sanitize device-bound arguments | Implemented · Tested (unit) | Package names validated before use; adb is always launched with an argument list, never a shell string |
| Show output and errors | Implemented · Tested (live, failure path) | stdout/stderr/exit code appended to the log; missing-tool message shown |
| Handle disconnects and timeouts | Implemented · Tested (unit, partial) | 30 s default timeout with cancellation; process kill on timeout. No device to disconnect. |
| Never execute silently | Implemented · Verified by code | Every command is written to the log before `Process.Start` |

### C. Fastboot Tools

| Feature | Status | Evidence / notes |
|---|---|---|
| Locate fastboot | Implemented · Tested (live, failure path) | Looks beside the configured `adb.exe`, then on `PATH`; absent tool produced an explicit message here |
| List fastboot devices | Implemented · Tested (unit) | `fastboot devices` parser; header and progress noise ignored |
| Read bootloader variables | Implemented · Tested (unit) | `getvar all`; `(bootloader)` prefix stripped, `getvar:`/`finished.` lines ignored, values sorted by name |
| Reboot to system / bootloader / recovery / fastbootd | Implemented · Tested (unit) | Argument builders tested; confirmation governed by Settings |
| Continue boot | Implemented · Tested (unit) | `fastboot continue` |
| Flash or erase partitions | **Blocked** | Requires bootloader-specific flashing and an unlocked bootloader; the page says so and no flash code path exists |

### D. Samsung Tools

| Feature | Status | Evidence / notes |
|---|---|---|
| Detect Samsung connection modes | Implemented · Tested (unit) | VID `04E8`, download mode by service/PID `685D`; no Samsung hardware present |
| Display available operations and required files | Implemented · Tested (live) | Requirements line rendered on the page |
| Validate firmware selections | Implemented · Tested (live + unit) | `.tar`/`.tar.md5` walked; MD5 verified. Live run on a generated `package.tar.md5`: “6 entries, 0 errors”, “MD5 check passed.” |
| Classify package components | Implemented · Tested (live + unit) | Entries tagged AP / BL / CP / CSC / HOME CSC / PIT / USERDATA; live run showed “Components: AP 1 · BL 1 · CP 1 · HOME CSC 1 · PIT 1 · Other 1” |
| Report progress and errors | Implemented · Tested (live) | Busy indicator, per-issue severity, log lines |
| Flash device | **Blocked** | Needs a licensed Odin protocol implementation and signing/PIT workflow that cannot be redistributed. The UI states this; there is no simulated flash path. |

### E. Qualcomm Tools

| Feature | Status | Evidence / notes |
|---|---|---|
| Detect EDL (9008) devices | Implemented · Tested (unit) | VID `05C6`, PID `9008`; no EDL hardware present |
| Display connection information | Implemented · Not tested | Rendered from the device scan; no device to show |
| Identify supported operations | Implemented · Tested (live) | Package inspection only, stated on the page |
| Validate required files | Implemented · Tested (live + unit) | `rawprogram*.xml`, `patch*.xml`, firehose programmer. Live run on a generated folder: “2 entries, 0 errors”, programmer listed. |
| Report unsupported operations clearly | Implemented · Tested (live) | Flashing notice rendered |
| Flash device | **Blocked** | Firehose programmers are proprietary per-OEM artifacts; no redistributable implementation is bundled. |

### F. MediaTek Tools

| Feature | Status | Evidence / notes |
|---|---|---|
| Detect MediaTek connection modes | Implemented · Tested (unit) | VID `0E8D`, preloader PID `2000`, modem/AT service |
| AT-command functionality | Implemented · Tested (unit, partial) | Transport opens one port per command at 115200 8N1, terminates on `OK`/`ERROR`. Parser and failure paths tested; **no live modem** |
| Device diagnostics | Implemented · Not tested | ATI, model, revision, serial, signal, registration. Serial/IMEI values are redacted in the log. |
| Connection management | Implemented · Tested (live) | Port list enumerated (`COM1`); preference honoured from Settings |
| Validate commands and handle failures | Implemented · Tested (unit) | Empty input and unusable port both returned typed errors |

### G. Spreadtrum Tools

| Feature | Status | Evidence / notes |
|---|---|---|
| Detect Spreadtrum devices | Implemented · Tested (unit) | VID `1782`; no hardware present |
| Display device information and operations | Implemented · Tested (live) | Page renders detection results, supported-operation text, and diagnostics |
| Diagnostics for unsupported devices | Implemented · Tested (live) | Explicit “no device detected” and flashing-unavailable text |
| Never simulate success | Verified by code | No flashing code path exists |

### H. Driver Manager

| Feature | Status | Evidence / notes |
|---|---|---|
| Inspect device drivers | Implemented · Tested (live) | Live run: “10 USB device(s) inspected, 0 with problems” |
| Display status and device information | Implemented · Tested (live) | Per-device problem text; empty state shown when clean |
| Identify missing/incompatible drivers | Implemented · Tested (unit) | Full `ConfigManagerErrorCode` mapping (0–49); no faulty device here |
| Installation instructions | Implemented · Tested (live) | Guidance panel rendered |
| Request permissions when required | Implemented · Tested (live) | Elevation state reported (“Not elevated…”), never forced |
| Never install drivers silently | Verified by code | No install/`SetupAPI` write path exists |

### I. Logs

| Feature | Status | Evidence / notes |
|---|---|---|
| Real-time log display | Implemented · Tested (live) | Activity panel and Logs page update live |
| Progress, warnings, errors | Implemented · Tested (live) | `DBG/INF/WRN/ERR` with per-level colour |
| Timestamps | Implemented · Tested (unit + live) | `HH:mm:ss.fff`, local time |
| Clear | Implemented · Tested (unit) | Service-level test; UI button not clicked |
| Export | Implemented · Tested (unit) | Header + lines written to `.log`; UI save dialog not exercised |
| No credentials in logs | Implemented · Tested (unit) | Labelled secrets, bearer tokens, IMEI/IMSI/ICCID redacted |

### J. Settings

| Feature | Status | Evidence / notes |
|---|---|---|
| Configure tool paths | Implemented · Tested (live) | Save wrote `%APPDATA%\Golem\TABLET\settings.json` and confirmed in the UI |
| Configure ADB and utilities | Implemented · Tested (live) | ADB path, preferred COM port, log directory |
| Save preferences locally | Implemented · Tested (live + unit) | Atomic temp-file write; round-trip unit test |
| Validate configuration values | Implemented · Tested (unit) | Timeout range, absolute paths, `adb.exe` name, `COM\d+` |
| Restore defaults | Implemented · Not tested | Confirmation dialog then re-save; dialog path not automated |

## 4. Explicitly excluded

Not present anywhere in the codebase, verified by repository-wide search for
network types (`HttpClient`, `WebClient`, `WebRequest`, `Socket`, `TcpClient`,
`UdpClient`):

- Automatic or mandatory update system, update server, update tab.
- Licence enforcement, licence server, subscription checks.
- Anti-tamper, anti-debug, obfuscation, packing.
- Telemetry, analytics, crash reporting.
- Any network client of the application's own. (The About page can hand a URL to
  the default browser only when the user clicks “Open GitHub profile”.)
