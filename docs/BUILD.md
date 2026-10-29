# Build and run

## 1. Prerequisites

| Requirement | Notes |
|---|---|
| Windows 10 1607 or later (x64) | WPF + `net8.0-windows` |
| .NET SDK 8.0.4xx | Verified with 8.0.427 |
| (optional) `adb.exe` | Point the app at it in Settings, or put it on `PATH` |
| (optional) `fastboot.exe` | Expected beside the configured `adb.exe`, or on `PATH` |

No Visual Studio is required. No NuGet credentials are required; both packages are
public.

> On the development machine used for this build the SDK is installed under the
> user profile and is **not** on `PATH`. Invoke it explicitly:
>
> ```powershell
> $dotnet = "$env:USERPROFILE\.dotnet\dotnet.exe"
> ```

## 2. Build

```powershell
$dotnet = "$env:USERPROFILE\.dotnet\dotnet.exe"

# Restore + build (Debug)
& $dotnet build .\Golem.Tablet.sln -c Debug

# Release build
& $dotnet build .\Golem.Tablet.sln -c Release
```

Output:

```text
src\Golem.Tablet\bin\Release\net8.0-windows\GolemTablet.exe
src\Golem.Tablet\bin\Release\net8.0-windows\Golem.Tablet.Core.dll
```

## 3. Test

```powershell
& $dotnet test .\Golem.Tablet.sln -c Release
```

Tests cover device classification, ADB argument construction and output parsing,
device-profile parsing, fastboot argument construction and output parsing, AT
response parsing and transport failures, settings round-trip and validation, log
redaction, log buffering/export, driver problem-code mapping, Samsung package
walking, MD5 trailer handling and component classification, and Qualcomm
firmware-package inspection. The suite currently contains 140 tests.

## 4. Run

```powershell
& $dotnet run --project .\src\Golem.Tablet\Golem.Tablet.csproj -c Debug
```

Or launch the built `GolemTablet.exe` directly.

The application does not require administrator rights to start. Driver inspection
reports when elevation is needed for full detail.

## 5. Publish a self-contained build

```powershell
& $dotnet publish .\src\Golem.Tablet\Golem.Tablet.csproj `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -o .\artifacts\win-x64
```

Self-contained output does not require the .NET desktop runtime on the target
machine. The output directory is ignored by git.

## 6. Continuous integration

`.github/workflows/build.yml` restores, builds, and tests on `windows-latest`
with the .NET 8 SDK. It does not publish artifacts and contains no deployment or
update step.

## 7. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `dotnet` reports "No SDKs were found" | Wrong host on `PATH`; call the SDK path explicitly. |
| `NETSDK1100: Windows is required` | Building on a non-Windows agent. Build on Windows. |
| No devices listed | Check the USB cable, then use Refresh. Some tablets only expose ADB after enabling developer options. |
| ADB not found | Set the full path to `adb.exe` in Settings. |
| Fastboot not found | Place `fastboot.exe` next to `adb.exe`, or on `PATH`. |
| AT port missing | Install the vendor modem driver; the port appears as a COM interface. |
| Driver page shows "elevation required" | Restart the app as administrator for complete driver status. |
