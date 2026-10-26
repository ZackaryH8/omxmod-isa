# omxmod-isa

Intelligent Speed Assistance for OMX. Requires **OMX API 2.1 or newer**; API 2.0 builds do not
expose the road data and input caps this resource needs.

A standalone client resource for OMX API 2.1. It follows the loaded map's road speed limits and
progressively caps accelerator input as the bus approaches the limit. It does not apply brakes,
change vehicle velocity, or edit any OMSI map, bus, model, or script files. It works in single
player and multiplayer; the dedicated server does not need this resource.

This models the accelerator-limiting behaviour of London's ISA. Downhill overspeed remains
possible, and entering a lower limit does not automatically slow the bus to that limit.
Reference: [TfL's explanation of ISA behaviour](https://www.london.gov.uk/who-we-are/what-london-assembly-does/questions-mayor/find-an-answer/isa-systems-tfl-buses-3).

## Build and install

Install the .NET 10 SDK. Set `OMX_SDK_DIR` to the directory in your OMX installation containing
`OMX.ModAPI.Shared.dll` and `OMX.ModAPI.Client.dll`. Only these public contracts are referenced;
no engine source or private repository access is required.

The reference icons are included. From this repository's root:

```sh
dotnet build IntelligentSpeedAssistance.csproj -c Release -p:OmxSdkDirectory="/path/to/OMX"
```

PowerShell can set `$env:OMX_SDK_DIR = 'C:\path\to\OMX'`. To reproduce the icon extraction,
run `bash tools/extract-icons.sh` with curl, OpenSSL and Poppler's `pdftoppm` installed
(Debian/Ubuntu: `sudo apt install curl openssl poppler-utils`). You can pass a local reference
PDF as the script's first argument for offline extraction. Windows users can run this optional
step through WSL or Git Bash with the same tools installed.
Inside an OMX source checkout, the project automatically references the public SDK projects.

Copy `bin/Release/net10.0/resource` into a directory named `IntelligentSpeedAssistance` under
your configured client resources directory (or select a resource root with `--resources`).
The package contains only the resource and its manifest/documentation; OMX supplies the SDK.
Unloading it automatically releases its accelerator cap.

## Live controls and display

The normal display is only a small transparent ISA symbol and speed sign in the bottom-right
corner. Hold **Ctrl** to reveal **On/Off** and **mph/km/h** controls. Both buttons show the
**current state**: `On` means ISA is enabled, and `mph` means the sign is displaying mph.
Click either button to change that setting. Disabling releases ISA's
accelerator cap on the next engine frame; enabling immediately evaluates the current road limit.
The enabled state and selected unit are saved in the resource's own
`preferences.json` and restored next time, overriding the manifest defaults.

The panel uses the actual green, white and amber ISA symbols extracted from page 44 of
[TfL New Bus Specification v2.7, ISA extract](https://foi.tfl.gov.uk/FOI-1516-2627/Bus%20Vehicle%20Specification_v2.7_ISA.pdf).
Green indicates an available/limiting system; white means disabled or road data unavailable; amber
means invalid operating data. These symbols remain resource assets, not hardcoded engine icons.
Their black background is removed by the generic HUD image colour-key option. The images are
included with their own attribution and are excluded from the MIT license; see
[asset provenance and terms](assets/README.md).
The speed sign is a blank red ring and white centre with the current number drawn separately.
Overspeed flashes the whole sign at 2 Hz; it produces no audible warning. The simulator's live
on/off control is a player setting, not a claim to reproduce real fleet restrictions on disabling ISA.

## Settings

Edit `configuration` in the installed `omx-resource.json` and reload the resource:

| Setting | Default | Behaviour |
| --- | --- | --- |
| `enabled` | `true` | Initial acceleration-limiting state, before saved preferences. |
| `speedUnit` | `mph` | Initial HUD units: `mph` or `km/h`. Map limits always use km/h internally. |
| `approachBandKmh` | `1.5` | Band below the limit over which available accelerator falls from full to zero. |
| `throttleRecoveryPerSecond` | `2.0` | How quickly accelerator availability returns after a restriction. |
| `showHud` | `true` | Shows the graphical display and live controls. |
| `roadDataGraceSeconds` | `5.0` | Retains the last known road limit through brief gaps in lane matching. |

Limits come from existing road rules, falling back to the map's default. AI curve/obstacle speeds
are excluded. Incorrect map rules can therefore produce incorrect limits, and visual speed signs
without corresponding road rules cannot be interpreted. A brief loss of road data retains the
last valid limit for five seconds; after that, the cap is released and the white unavailable
symbol appears. Changing vehicle, map or session clears the retained limit immediately.
Matching checks lane direction and road height. Cap requests from
other resources continue to apply independently; ISA cannot release another resource's limit.

The bus speedometer's unit does not change the limiter. Velocity is measured in metres per
second and converted to km/h for comparison with map road rules. The unit button converts
only the sign's displayed number: a **50 km/h** rule displays **31 mph** in mph mode, or **50**
in km/h mode, with the same physical limit in both modes. An mph bus on a km/h map works the
same way; select whichever display unit you want. Map values are never reinterpreted as mph.

The engine exposes generic `ILocalVehicle.Road`, `IClientInput.ThrottleLimit`, resource images and
circular badges. All ISA thresholds, tapering, recovery and presentation live in this resource.

## Development

```sh
dotnet test tests/Isa.Tests/Isa.Tests.csproj -c Release
```

These tests cover the accelerator policy, road-data grace period and configuration validation.
They do not need OMX or the reference images. CI runs them on Linux and Windows. A full resource
build requires the real API 2.1 SDK. To test in game, copy the built package
into your resource root and restart OMX; enable/disable and units can then be changed live.

Report bugs through [GitHub issues](https://github.com/ZackaryH8/omxmod-isa/issues), including the
OMX version, map, vehicle, selected units and steps to reproduce. Do not upload credentials,
your saved preferences, the SDK binaries, or OMSI content.

## Releases

Pushing a version tag such as `v1.0.3` runs the Release workflow. First set `version` in
`omx-resource.json` to the same version and commit the change. The workflow runs the policy
tests, builds against the public SDK, and publishes `omxmod-isa-1.0.3.zip` and its SHA-256
checksum to GitHub Releases. The ZIP includes the mod, icons, documentation and attribution.
SDK assemblies and debug symbols are excluded.

Maintainers configure the build-only SDK once, or update it when a newer API is required.
With Python 3, an authenticated GitHub CLI and repository admin access:

```sh
python3 tools/configure-release-sdk.py /path/to/OMX --repo ZackaryH8/omxmod-isa
```

This stores the two SDK assemblies in the `OMX_SDK_ARCHIVE` Actions secret without publishing
them or granting the workflow access to the private engine repository. Release permissions
are limited to the publish job. Use the workflow's **Run workflow** button with an existing tag
to retry a release; reruns replace its ZIP and checksum while preserving its release notes.

## License

Project code, scripts and documentation are [MIT licensed](LICENSE). OMX, OMSI and TfL material
retain their own rights. The included TfL reference images are excluded from MIT; their
attribution and rights status are recorded in [assets/README.md](assets/README.md). This repository
does not distribute game content or SDK binaries and does not claim TfL certification or endorsement.
