# PHOODAB

## Repository Layout

- `phoodab/backend` - .NET 10 backend solution and tests
- `phoodab/apps/web` - React shell consuming generated API client
- `phoodab/apps/mobile` - mobile placeholder
- `phoodab/packages/api-client` - TypeScript client generation package
- `phoodab/docs` - architecture and ADRs

## Quick Start

### 1) Run backend API

```bash
cd phoodab/backend/src/Phoodab.Api
dotnet run
```

API endpoints:
- `http://localhost:5199/health`
- `http://localhost:5199/version`
- `http://localhost:5199/swagger`

### 2) Generate TypeScript API client

```bash
cd phoodab/packages/api-client
npm install
npm run generate
```

### 3) Run web shell

```bash
cd phoodab/apps/web
npm install
npm run dev
```

The web shell calls `/health` and `/version` through `@phoodab/api-client` generated functions.

## Manual CI Fallback

CI normally runs for pull requests and pushes to `main`. If a pull request branch update does not create a CI run, trigger the same workflow manually:

```bash
gh workflow run CI --repo yaron-E92/PHOODAB --ref <pr-branch>
gh run list --repo yaron-E92/PHOODAB --branch <pr-branch> --limit 5
```

## MVP Demo Flow

- Demo data is opt-in and development-only. From `phoodab/backend/src/Phoodab.Api`, create it without overwriting an existing local store:
  ```bash
  ASPNETCORE_ENVIRONMENT=Development DemoData__Mode=Seed dotnet run
  ```
- To discard local changes and recreate the same canonical demo household, stop the API and run:
  ```bash
  ASPNETCORE_ENVIRONMENT=Development DemoData__Mode=Reset dotnet run
  ```
- `Seed` only writes when the local store is completely empty. `Reset` replaces the local MVP store with fixed IDs and dates relative to the current UTC day, so the journeys remain repeatable. After reset, use `Seed` for normal starts if desired.
- `DemoData__Mode` accepts only `Seed` or `Reset`. The API ignores it outside the Development environment, protecting production data from demo seeding and resets.
- Start web (`npm run dev` in `phoodab/apps/web`).
- Open the app and review the low Milk stock, expired Greek Yogurt, soon-expiring Eggs, and replenishment suggestions.
- Review shopping rows in **Shopping List**, **In Cart / Buying**, and **Stock Update Needed** states.
- Browse **Maple House → Kitchen → Pantry Cabinet → Eye-level Shelf** to see consumables and durable items together.
- Review the Stand Mixer warranty and the Toaster in **NeedsRepair** status.
