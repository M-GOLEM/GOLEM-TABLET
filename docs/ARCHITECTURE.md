# Architecture

## 1. Purpose

GOLEM TABLET is a Windows desktop application for tablet device management and
diagnostics. It provides device discovery, ADB tooling, AT-command diagnostics,
driver inspection, firmware-package validation, and a live activity log.

This document covers two things:

1. The **reference application** that was studied for behavioural requirements.
2. The **GOLEM TABLET architecture**, which is an independent implementation.

> The reference application is a third-party, commercially licensed product that
> was decompiled for study. None of its source code, compiled BAML resources, icons,
> fonts, or bundled binaries are used in GOLEM TABLET. See `DEPENDENCIES.md` for the
> component-by-component audit.

## 2. Reference application (analysis only)

| Attribute | Observation |
|---|---|
| Language / UI | C# / WPF, UI compiled to BAML (no XAML files), WinForms interop |
| Framework | .NET Framework 4.8, x64, `requireAdministrator` manifest |
| Pattern | MVVM via CommunityToolkit.Mvvm; a single very large `MainViewModel` |
| Device families | Samsung (Odin), Qualcomm EDL (9008), Spreadtrum, MediaTek (AT), ADB |
| Services | ADB runner, AT/COM runner, Odin coordinator, SPRD coordinator, device-mode detector, driver fixer, remote asset store |
| Infrastructure | USB via LibUsbDotNet/Vanara, JSON, LZ4, BouncyCastle |
| Obfuscation | Control-flow flattening, string encryption, anti-tamper, licensing/update gates |

Behavioural conclusions drawn from the reference and carried into requirements:

- Device families are distinguished by USB VID/PID and by which driver service binds.
- Long-running work must stay off the UI thread; operations are cancellable.
- Firmware packages are validated before any device operation is attempted.
- The log is the primary feedback surface, with severity and timestamps.

The reference's licensing, mandatory-update, telemetry, and anti-tamper mechanisms
are **not** reproduced. GOLEM TABLET has no network access of any kind.

## 3. GOLEM TABLET solution layout

```text
golem-tablet/
├── Golem.Tablet.sln
├── src/
│   ├── Golem.Tablet.Core/        class library (net8.0-windows)
│   │   ├── Adb/                  process runner + output parsing
│   │   ├── At/                   serial AT-command transport + parsing
│   │   ├── Configuration/        JSON settings store, defaults, validation
│   │   ├── Devices/              USB enumeration + classification
│   │   ├── Drivers/              driver/device problem inspection
│   │   ├── Fastboot/             fastboot runner + device/variable parsing
│   │   ├── Logging/              log model, sink, redaction
│   │   ├── Qualcomm/             EDL firmware-package inspection
│   │   ├── Samsung/              Odin firmware-package inspection
│   │   └── Mvvm/                 minimal ObservableObject / commands
│   ├── Golem.Tablet/             WPF application (net8.0-windows)
│   │   ├── App.xaml(.cs)         composition root
│   │   ├── MainWindow.xaml(.cs)  shell: navigation + log panel
│   │   ├── ViewModels/           one view model per module
│   │   ├── Views/                one UserControl per module
│   │   └── Styles/               theme resources
│   └── Golem.Tablet.Tests/       xUnit test project
├── docs/
├── .github/workflows/build.yml
├── README.md
├── LICENSE
└── .gitignore
```

### Dependency rule

`Golem.Tablet` → `Golem.Tablet.Core`. Core never references WPF. All UI-thread
marshalling crosses a single `IUiDispatcher` abstraction implemented by the app,
so core logic is testable without a UI.

## 4. Framework choice

The reference targets .NET Framework 4.8. GOLEM TABLET targets **.NET 8
(`net8.0-windows`)** instead. Rationale:

- The available toolchain is the .NET 8 SDK; there is no Visual Studio or .NET
  Framework targeting pack on the build machine.
- A new codebase needs none of the net48 compatibility shims the reference
  required (Span, ValueTask, Index/Range, `IsExternalInit`, etc.).
- `System.Management` and `System.IO.Ports` ship as first-class packages.
- Windows 10 1607+ is required, which covers every Windows version that still
  receives support and that hosts USB flashing tooling in practice.

The app is published self-contained (`win-x64`) so target machines do not need a
runtime pre-installed.

## 5. Startup flow

```text
GolemTablet.exe
  └─ App.OnStartup
       ├─ construct services: SettingsStore, LogService, DeviceService,
       │   AdbService, FastbootService, AtCommandService, DriverInspector
       ├─ load settings (JSON, %APPDATA%\Golem\TABLET\settings.json)
       ├─ construct MainViewModel + module view models
       ├─ construct MainWindow, set DataContext
       └─ show window
```

There is no single-instance mutex, no startup gate, no update check, and no
network call. Settings load failures fall back to defaults and are logged.

## 6. Threading model

- All device, process, and serial operations are `async` and cancellable.
- `AsyncRelayCommand` prevents re-entrancy while an operation runs.
- `LogService` accepts an `IUiDispatcher`; it marshals collection changes to the
  UI thread and applies a bounded buffer so a chatty device cannot exhaust memory.
- `Process` output is read asynchronously with a timeout and `CancellationToken`.

## 7. Error handling and logging

- Device and tool failures return typed results (`ToolResult`, `DeviceInfo`,
  `DriverIssue`) rather than throwing across module boundaries.
- Every user action and every failure is written to the log with a timestamp and
  severity. Commands are logged before execution, never silently.
- `LogRedactor` removes likely secrets (`password=`, `token=`, authorization
  headers, long hex keys) before a line is stored or exported.

## 8. Security posture

| Property | GOLEM TABLET behaviour |
|---|---|
| Network access | None. No HTTP client, no update service, no telemetry. |
| Elevation | `asInvoker`. Driver module reports when admin is required. |
| Driver installation | Never automatic. The app explains what to install and where. |
| Command execution | `ProcessStartInfo.ArgumentList` only (no shell string concatenation). |
| Secrets in logs | Redacted by pattern before storage and export. |
| File input | Validated for existence, extension, size, and header signature. |
| Third-party code | Only permissively licensed packages; no copied binaries. |

## 9. Design decisions

- **Minimal MVVM instead of a toolkit.** `ObservableObject` and the command types
  are ~150 lines; a dependency is not justified.
- **No DI container.** The composition root wires ~10 services explicitly.
- **Typed models over exceptions** at module boundaries.
- **One view model per module**, each independently testable.
- **Device classification is pure logic** (`DeviceClassifier`) so it can be
  unit-tested against a table of VID/PID/name/service combinations.

## 10. Extension points

| To add | Where |
|---|---|
| A new device family | `Devices/DeviceKind`, `DeviceClassifier`, a `ViewModels/*ViewModel`, a `Views/*View` |
| A new ADB operation | `AdbActions` argument builder + a command in the ADB view model |
| A new fastboot operation | `FastbootActions` argument builder + a command in the fastboot view model |
| A new AT command | `AtCommandService` method or a preset in the MediaTek view model |
| A new firmware validator | A new inspector in Core + tests |
| A new UI page | `Views/`, `ViewModels/`, and one navigation entry in `MainViewModel` |
