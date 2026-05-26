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

- Start backend (`dotnet run` in `phoodab/backend/src/Phoodab.Api`) with `ASPNETCORE_ENVIRONMENT=Development` to auto-seed Milk, Eggs, Pasta, and Rice demo data.
- Start web (`npm run dev` in `phoodab/apps/web`).
- Open the app and review **Inventory Summary**, **Expiring / Expired Lots**, and **Replenishment Suggestions**.
- In **Replenishment Suggestions**, click **Add to Shopping List**.
- In **Shopping List**, click **Mark Purchased** for the created row.
- Expected result: suggestion becomes a shopping-list item and updates to Purchased/Resolved.
