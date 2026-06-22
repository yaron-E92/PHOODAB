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

The current views stay host-specific. SecondBrain integration should reference
`Phoodab.Mobile.Shared` for shared services and ViewModel-facing composition,
then provide its own shell, navigation, resources, and views where the
experience diverges.

The standalone host uses the shared PHOODAB application services in-process for
normal local pantry data, so it does not require a separately hosted backend for
the current MVP flows. Android emulator host networking
(`http://10.0.2.2:5199`) can remain a development fallback for future
remote/API adapter testing.

Build from the repository root with:

```powershell
dotnet build .\phoodab\apps\mobile\Phoodab.Mobile.csproj
```
