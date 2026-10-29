# Dependencies and licence audit

## 1. Libraries used by GOLEM TABLET

All are permissively licensed and consumed as NuGet packages. No binaries are
vendored into the repository.

| Package | Version | Licence | Purpose |
|---|---|---|---|
| `System.Management` | 8.0.0 | MIT | WMI queries for USB device and driver inspection |
| `System.IO.Ports` | 8.0.0 | MIT | Serial transport for AT commands |
| `.NET 8` runtime + WPF | 8.0.x | MIT | Application framework |

Transitive packages pulled by the two NuGet references (e.g. `System.CodeDom`) are
Microsoft-published and MIT-licensed.

### Test-only

| Package | Version | Licence | Purpose |
|---|---|---|---|
| `Microsoft.NET.Test.Sdk` | 17.11.1 | MIT | Test host |
| `xunit` | 2.9.2 | Apache-2.0 | Test framework |
| `xunit.runner.visualstudio` | 2.8.2 | Apache-2.0 | Test discovery |

None of the test packages ship in the application output.

## 2. Components from the reference project — audit and rejection

The reference project bundled or contained the following. Each was evaluated and
**not** carried over.

| Component | Nature | Licence status | Decision |
|---|---|---|---|
| `haafedk-tablet-engine.dll` (renamed `golem-tablet-engine.dll`) | Closed application engine, no source in tree | Proprietary, unverified | **Excluded.** Core device logic; cannot be redistributed or rebuilt. |
| `QCEDL.NET.dll` | Qualcomm EDL client | Not stated in the tree | **Excluded.** Would be reconsidered only after verifying a permissive licence. |
| `SharpOdinClient\*.cs` | Samsung Odin protocol client | Not stated in the tree | **Excluded.** Protocol code copied from a third party; not reused. |
| `SPRDClientCore.*` | Spreadtrum protocol code | Not stated in the tree | **Excluded.** Same reasoning. |
| `LibUsbDotNet` | USB library | LGPL family | **Excluded.** Not required; avoided to keep licensing simple. |
| `Vanara.*` | Win32 interop | MIT | **Excluded.** Not required. |
| `BouncyCastle.Cryptography` | Cryptography | MIT | **Excluded.** Not required. |
| `K4os.Compression.LZ4*` | Compression | MIT | **Excluded.** Not required. |
| `CommunityToolkit.Mvvm` | MVVM toolkit | MIT | **Excluded.** Replaced by ~150 lines of local MVVM. |
| `Resources\*.g.resources` (BAML, images, fonts) | Compiled UI and assets | Copyrighted | **Not copied.** The GOLEM UI is written from scratch. |
| `app.ico` | Application icon | Copyrighted | **Not copied.** A new icon is generated for GOLEM. |
| `adb.exe` | Android platform tool | Not bundled | **Not bundled.** The user configures a local ADB path. |
| `libusb-1.0.dll` | Native USB library | LGPL-2.1 | **Not bundled.** Not required by the current feature set. |

## 3. Attribution obligations

- `System.Management`, `System.IO.Ports`, and the .NET runtime are MIT-licensed;
  the MIT notice is satisfied by including the licence text in
  `THIRD-PARTY-NOTICES.md`.
- xUnit is Apache-2.0; its notice is included in `THIRD-PARTY-NOTICES.md`.

## 4. Assets

- The GOLEM TABLET icon is newly generated for this project.
- No fonts, images, or other assets are taken from the reference application.
