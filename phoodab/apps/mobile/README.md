# PHOODAB Mobile

Initial .NET MAUI frontend for the PHOODAB pantry MVP.

The app mirrors the current web MVP at functional depth:

- local health/version startup checks
- consumable item creation
- consumable entry creation and lot audit actions
- expiring consumable visibility
- replenishment suggestions and shopping-list handoff
- shopping-list status actions
- replenishment rule editing

The app uses the shared PHOODAB application services in-process for normal
local pantry data, so it does not require a separately hosted backend for the
current MVP flows. Android emulator host networking (`http://10.0.2.2:5199`)
can remain a development fallback for future remote/API adapter testing.

Build from the repository root with:

```powershell
dotnet build .\phoodab\apps\mobile\Phoodab.Mobile.csproj
```
