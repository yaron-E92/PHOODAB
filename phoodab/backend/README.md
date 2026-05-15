# PHOODAB Backend

## Projects

- `src/Phoodab.Api` - minimal API, OpenAPI, health/version endpoints
- `src/Phoodab.Application` - application layer
- `src/Phoodab.Domain` - domain layer
- `src/Phoodab.Infrastructure` - infrastructure layer
- `tests/*` - test projects

## Build

```bash
cd phoodab/backend
dotnet build Phoodab.sln
```

## Run API

```bash
cd phoodab/backend/src/Phoodab.Api
dotnet run
```

## Smoke checks

```bash
curl http://localhost:5199/health
curl http://localhost:5199/version
curl http://localhost:5199/swagger/v1/swagger.json
```

## Generate API client

```bash
cd phoodab/packages/api-client
npm run generate
```
