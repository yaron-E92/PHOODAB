# PHOODAB Mobile

Initial .NET MAUI frontend for the PHOODAB pantry MVP.

The app mirrors the current web MVP at functional depth:

- health/version startup checks
- consumable item creation
- consumable entry creation and lot audit actions
- expiring consumable visibility
- replenishment suggestions and shopping-list handoff
- shopping-list status actions
- replenishment rule editing

The default API base URL is `http://localhost:5199`. When running against an
Android emulator, use `http://10.0.2.2:5199` in the API base URL field so the
emulator can reach the host backend.

Build from the repository root with:

```powershell
dotnet build .\phoodab\apps\mobile\Phoodab.Mobile.csproj
```
