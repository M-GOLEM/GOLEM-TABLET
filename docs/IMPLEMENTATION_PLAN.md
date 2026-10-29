# Implementation plan

## 1. Scope

Build an independent GOLEM TABLET application that reproduces the *functional*
behaviour of the reference tool where it can be done honestly, and explicitly
reports anything that cannot.

In scope:

- Device discovery and classification (ADB, Samsung, Qualcomm EDL, MediaTek,
  Spreadtrum, unknown USB).
- ADB tooling with curated operations.
- AT-command diagnostics over serial.
- Driver and device problem inspection (read-only).
- Firmware-package validation for Samsung and Qualcomm packages.
- Live logging with redaction; local JSON settings.
- A new WPF UI using the GOLEM identity.

Out of scope, by requirement:

- Update system, telemetry, licence enforcement, anti-tamper, obfuscation.
- Any network access.

Blocked, and reported as such in the UI rather than faked:

- Samsung flashing, Qualcomm EDL flashing, Spreadtrum flashing. Each needs a
  protocol implementation or per-OEM binary whose licence cannot be established
  from the reference material.

## 2. Phases and acceptance criteria

| Phase | Work | Acceptance |
|---|---|---|
| 1 | Analysis and documentation | `docs/` set written; architecture, feature inventory, licence audit complete |
| 2 | Solution scaffold | `dotnet build` succeeds from a clean checkout |
| 3 | Core services | Each service has a typed interface and pure-logic parts are unit-tested |
| 4 | WPF shell and module views | App launches, navigation works, log shows live entries |
| 5 | Tests and verification | `dotnet test` green; app starts; no network/update code |
| 6 | Repository packaging | README, LICENSE, notices, `.gitignore`, CI workflow; secret/artifact scan clean |

## 3. Work breakdown

### Phase 3 — Core

1. MVVM primitives (`ObservableObject`, `RelayCommand`, `AsyncRelayCommand`).
2. Logging (`LogLevel`, `LogEntry`, `LogRedactor`, `LogService`, `IUiDispatcher`).
3. Configuration (`AppSettings`, `SettingsStore`, validation, defaults, reset).
4. Devices (`DeviceFamily`, `ConnectionMode`, `UsbDeviceRecord`, `DeviceClassifier`,
   `WindowsDeviceEnumerator`, `DeviceService`).
5. ADB (`IProcessRunner`, `ProcessRunner`, `AdbDevice`, `AdbOutputParser`,
   `AdbService`).
6. AT (`AtResponse`, `AtResponseParser`, `AtCommandService`).
7. Drivers (`DriverIssue`, `DriverInspector`, problem-code mapping).
8. Firmware inspectors (Samsung TAR/tar.md5; Qualcomm rawprogram/patch XML).

### Phase 4 — UI

1. Theme resources and shared styles.
2. Shell with left navigation and a collapsible log panel.
3. One view + view model per module.
4. Settings persistence and About dialog.

### Phase 5 — Verification

1. Unit tests per pure component.
2. `dotnet build` and `dotnet test` in Release.
3. Launch the executable and confirm the window starts, the log records startup,
   and navigation renders.
4. Static check: no `HttpClient`/`WebClient`/`Socket` usage; no updater types.
5. Re-verify the status column in `FEATURES.md` against actual results.

## 4. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Hardware-dependent features cannot be exercised here | Classifiers and parsers are unit-tested with captured sample output; hardware paths are reported as "not exercised in this environment". |
| Configurable ADB path may be invalid | Resolve, validate, and log a clear error; fall back to `PATH`. |
| Serial port busy or absent | Typed error, no hang (timeout + cancellation). |
| Large firmware files | Inspect headers/streams without loading whole files; MD5 optional and cancellable. |
| Scope creep into flashing | Flashing is out of scope and surfaced as blocked. |

## 5. Definition of done

- Clean Release build with zero errors.
- All automated tests pass.
- Application starts and shuts down cleanly.
- Documentation matches the delivered behaviour.
- No copied proprietary code, assets, or binaries.

## 6. Completion record

| Phase | Result |
|---|---|
| 1 | `docs/ARCHITECTURE.md`, `FEATURES.md`, `DEPENDENCIES.md`, `BUILD.md`, `IMPLEMENTATION_PLAN.md` written |
| 2 | Solution builds from clean: `Release`, 0 warnings, 0 errors |
| 3 | Core implemented and unit-tested (140 tests) |
| 4 | Eleven pages render; navigation, live log, settings persistence verified by UI automation |
| 5 | `dotnet test` 140/140; app launched, inspected a generated `.tar.md5` and an EDL folder, inspected drivers, saved settings, exited cleanly; self-contained publish launched (title `GOLEM TABLET`, icon in title bar, exit code 0); no network types present |
| 6 | README, MIT LICENSE, third-party notices, changelog, `.gitignore`, CI workflow added; no secrets or build output committed |

Deviations from the plan:

- The framework is `net8.0-windows` rather than .NET Framework 4.8. Rationale is in
  `ARCHITECTURE.md` §2: no 4.8 targeting pack in the environment, no need for
  compatibility shims, and self-contained publish stays a single-folder deployment.
- Package-inspection results are shown inline on the Samsung and Qualcomm pages
  instead of a separate progress dialog; long-running work runs off the UI thread
  with a busy indicator.
