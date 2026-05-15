# PHOODAB

## Repository Layout

- `phoodab/backend` - .NET 8 backend solution and tests
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
