# PHOODAB CI

PHOODAB CI validates the repository as a monorepo rather than treating the root as one Node or .NET project. The workflow uses path-aware product surfaces and finishes with one stable `CI gate` that requires every applicable surface to succeed.

## Product surfaces

- Backend: `phoodab/backend/Phoodab.sln` restore, build, tests, and TRX results.
- Web: the root npm workspace install plus TypeScript, Vitest, and Vite production build for `phoodab/apps/web`.
- API client: build and start the local API, generate `phoodab/packages/api-client/src/generated.ts` twice, require deterministic output, and fail on committed-client drift.
- Mobile shared/headless: build `Phoodab.Mobile.Shared.csproj` and the non-MAUI `net10.0` shape of `Phoodab.Mobile.csproj`.
- MAUI Android: install the Android workload, restore, build `net10.0-android`, and run the repository Android doctor check.
- MAUI Windows: install MAUI, restore, build `net10.0-windows10.0.19041.0`, and require the built application assembly.

Workflow changes run all product surfaces. Backend changes also run API-client and mobile validation because the client and mobile-shared layers depend on backend contracts. Mobile-shared changes run both shared/headless and MAUI product validation. Docs-only changes keep repository and workflow-policy validation but do not manufacture unrelated product work.

## Local equivalents

Backend:

```bash
dotnet restore phoodab/backend/Phoodab.sln
dotnet build phoodab/backend/Phoodab.sln --configuration Debug --no-restore
dotnet test phoodab/backend/Phoodab.sln --configuration Debug --no-build --no-restore
```

Web:

```bash
npm ci
npm exec --workspace phoodab/apps/web -- tsc --noEmit
npm test --workspace phoodab/apps/web
npm run build --workspace phoodab/apps/web
```

API-client drift requires the API to be available at `http://localhost:5199`:

```bash
npm ci
dotnet restore phoodab/backend/src/Phoodab.Api/Phoodab.Api.csproj
dotnet build phoodab/backend/src/Phoodab.Api/Phoodab.Api.csproj --configuration Debug --no-restore
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5199 dotnet run --project phoodab/backend/src/Phoodab.Api/Phoodab.Api.csproj --configuration Debug --no-build --no-restore
npm run generate --workspace phoodab/packages/api-client
git diff --exit-code -- phoodab/packages/api-client/src/generated.ts
```

Mobile shared/headless:

```bash
dotnet restore phoodab/apps/mobile-shared/Phoodab.Mobile.Shared.csproj -p:CanTargetAndroid=false -p:CanTargetWindows=false
dotnet build phoodab/apps/mobile-shared/Phoodab.Mobile.Shared.csproj --configuration Debug --no-restore -p:CanTargetAndroid=false -p:CanTargetWindows=false
dotnet restore phoodab/apps/mobile/Phoodab.Mobile.csproj -p:CanTargetAndroid=false -p:CanTargetWindows=false -p:EnableMauiTargets=false
dotnet build phoodab/apps/mobile/Phoodab.Mobile.csproj --configuration Debug --no-restore -p:CanTargetAndroid=false -p:CanTargetWindows=false -p:EnableMauiTargets=false
```

Android:

```bash
dotnet workload install maui-android --skip-manifest-update
dotnet restore phoodab/apps/mobile/Phoodab.Mobile.csproj
bash phoodab/apps/mobile/scripts/maui-android-ubuntu.sh build -c Debug --no-restore
bash phoodab/apps/mobile/scripts/maui-android-ubuntu.sh doctor
```

Windows:

```powershell
dotnet workload install maui --skip-manifest-update
dotnet restore phoodab/apps/mobile/Phoodab.Mobile.csproj
dotnet build phoodab/apps/mobile/Phoodab.Mobile.csproj --configuration Debug --no-restore -f net10.0-windows10.0.19041.0
```

The CI package-feed setup uses the repository `GITHUB_TOKEN` with `packages: read`; it does not depend on a date-stamped personal-access-token secret.
