# PHOODAB Mobile

Standalone .NET MAUI frontend for the PHOODAB pantry MVP.

The app mirrors the current web MVP at functional depth:

- local health/version startup checks
- consumable item creation
- consumable entry creation and lot audit actions
- expiring consumable visibility
- replenishment suggestions and shopping-list handoff
- shopping-list status actions
- replenishment rule editing

## Presentation boundaries

The standalone runnable host lives in this project. It owns:

- `App` and MAUI host startup
- the current code-built `MainPage`
- standalone shell/navigation and page flow
- standalone visual resources and platform integrations

Reusable presentation composition lives in `../mobile-shared`. That project
owns application-facing dependency registration and future shared ViewModel
composition that can be referenced by both this standalone host and a
SecondBrain host.

## Current presentation inventory

| Area | Standalone host ownership |
| --- | --- |
| Views | `MainPage` is host-specific and remains in this project. It owns the current dashboard, inventory, shopping, location, and durable-item screens as code-built MAUI controls. |
| ViewModels | No separate reusable ViewModel classes exist yet. Presentation state currently embedded in `MainPage` should move to `../mobile-shared` only when it becomes host-neutral. |
| Navigation | `App` wraps the standalone `MainPage` in a `NavigationPage`; `MainPage` owns the current page switching controls. |
| Resources | Standalone colors, labels, visual choices, and future MAUI resources belong here. |
| Platform services | Android and Windows startup files and app identifiers belong here. |
| Startup wiring | `MauiProgram` builds the standalone MAUI app, calls `AddPhoodabSharedPresentation`, and registers standalone pages. |

The current views stay host-specific. SecondBrain integration should reference
`Phoodab.Mobile.Shared` for shared services and future ViewModel-facing
composition, then provide its own shell, navigation, resources, platform
integrations, and views where the experience diverges.

The standalone host uses the shared PHOODAB application services in-process for
normal local pantry data, so it does not require a separately hosted backend for
the current MVP flows. Android emulator host networking
(`http://10.0.2.2:5199`) can remain a development fallback for future
remote/API adapter testing.

## Target frameworks and host support

`Phoodab.Mobile.csproj` enables MAUI targets only when the required local
tooling is present:

- Android: `net10.0-android` when the .NET MAUI SDK pack and Android SDK are
  installed. This is the supported Ubuntu development path.
- Windows: `net10.0-windows10.0.19041.0` when the MAUI SDK pack is available
  on Windows.
- Fallback: plain `net10.0` when MAUI or Android/Windows prerequisites are
  missing, using `MauiWorkloadPlaceholder.cs` so non-MAUI verification can
  still complete.

iOS and MacCatalyst are native-host targets and are not configured for Ubuntu.
If they are added later, they should be built from macOS with the corresponding
Apple tooling.

## Ubuntu Android build and run

Ubuntu does not provide a native Linux desktop target for this app. Use the
Android target with the repo helper:

```bash
bash phoodab/apps/mobile/scripts/maui-android-ubuntu.sh doctor
bash phoodab/apps/mobile/scripts/maui-android-ubuntu.sh build -c Debug
bash phoodab/apps/mobile/scripts/maui-android-ubuntu.sh run -c Debug
```

`doctor` checks for:

- `dotnet` on `PATH`
- the `maui-android` or full `maui` workload
- `ANDROID_SDK_ROOT` or `ANDROID_HOME` pointing at an installed Android SDK

`run` also requires Android SDK platform-tools and at least one emulator or USB
device in the `device` state from `adb devices`.

If a prerequisite is missing, the helper stops before invoking MSBuild and
prints the setup action to take. Typical Ubuntu setup is:

```bash
dotnet workload install maui-android
export ANDROID_SDK_ROOT="$HOME/Android/Sdk"
```

Then install Android SDK platform-tools and an Android platform through Android
Studio or `sdkmanager`, and start an emulator or attach a debug-enabled device
before using `run`.

Build from the repository root on Windows with:

```powershell
dotnet build .\phoodab\apps\mobile\Phoodab.Mobile.csproj
```
