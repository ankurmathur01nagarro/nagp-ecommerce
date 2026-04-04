# nagp-ecommerce Project Guidelines

## Repository Layout

```
src/                          ← git root
├── api/                      ← .NET solution (CLAUDE.md here for build commands)
│   ├── ECOM.WebApi/          ← Main API + YARP proxy + SPA static file serving
│   ├── ECOM.Identity.Api/    ← OpenIddict auth server (AddClient + AddServer)
│   └── ECOM.WebApi.Data/     ← Shared EF Core DbContext, models, migrations
├── ui/                       ← Angular 21 SPA
├── deployment/               ← Kustomize overlays (base/overlays/local|cloud)
├── Dockerfile.webapi         ← Unified 3-stage image: ng-build → dotnet-build → aspnet runtime
└── .github/workflows/        ← GitHub Actions CI/CD
```

See `api/CLAUDE.md` for full build/run commands and architecture overview.

## Tech Stack

- **.NET 10**, **PostgreSQL 17**, **EF Core** (Npgsql provider)
- **OpenIddict 7.4.0** — Identity server (AddServer) + Google OAuth client (AddClient) in one process
- **YARP 2.3.0** — Embedded reverse proxy inside WebApi; routes browser-facing Identity endpoints
- **Angular 21** — SPA served from WebApi's `wwwroot` via `UseStaticFiles` + `MapFallbackToFile`
- **Gateway API HTTPRoute** (`gateway.networking.k8s.io/v1`) — not nginx Ingress
- **Kustomize** — base/overlays pattern; ArgoCD/Flux GitOps delivery
- **External Secrets Operator** — reads from Azure Key Vault (`kv-nagp-ecom-secrets`)

## Key Conventions

### .NET
- **One type per file** — every `record`, DTO, and result type has its own `.cs` file
- **Centralized NuGet versions** — all versions in `Directory.Packages.props`; never specify versions in individual `.csproj` files
- **`KnownIPNetworks.Clear()`** (not `KnownNetworks`) — use this for ForwardedHeaders in .NET 10
- **`ASPNETCORE_ENVIRONMENT: Kubernetes`** — third appsettings tier; `appsettings.Kubernetes.json` loaded in K8s pods
- `ConnectionStrings` key is **`PostgresConnString`** in WebApi, **`Default`** in Identity API and MigrationJob

### YARP Config (appsettings)
- Routes and Clusters use **JSON objects** (keyed by name), not arrays — this means `appsettings.Development.json` additively merges with base, not replaces
- `web-portal` catch-all route (`/{**catch-all}` → `localhost:4200`) lives **only in `appsettings.Development.json`** — absent in K8s/Compose so requests fall through to `UseStaticFiles`
- YARP routes for Identity API: `/api/connect/authorize` (PathRemovePrefix `/api`) and `/api/auth/google/callback` (no transform — path must match OpenIddict's registered callback URI exactly)

### Google OAuth Flow (OpenIddict Mimban pattern)
```
Browser → /api/auth/external/challenge (WebApi)
        → /api/connect/authorize (YARP → Identity)
        → Google consent
        → /api/auth/google/callback (YARP → Identity; OpenIddict intercepts here)
        → /api/auth/external/complete (WebApi; exchanges code for JWT)
        → SPA /auth/callback?token=...
```
- OpenIddict's `AuthenticateAsync(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme)` only works when the request path **exactly matches** the URI passed to `SetRedirectUri` — path mismatches produce `InvalidOperationException: no OpenIddict client context`
- `SetRedirectUri` in `OpenIddictExtensions.cs` reads from `ExternalAuth:Google:CallbackUri` config
- `IdentityApi:CompleteCallbackUri` must be set explicitly in every environment — it must match what Google was told in the challenge step

### Angular
- All environment files (`environment.development.ts`, `environment.ts`, `environment.kubernetes.ts`) — `webApiBaseUrl: ""` (empty = same-origin relative paths). In dev, YARP's `web-portal` catch-all proxies Angular (`ng serve` on `:4200`) behind `localhost:5001`, so all API calls and redirects are same-origin and CORS is not needed.
- `loginWithGoogle()` navigates to `${webApiBaseUrl}/api/auth/external/challenge` — kicks off full browser redirect flow

## Environments

| Environment | Angular served by | Entry point | SpaBaseUrl |
|---|---|---|---|
| Local dev | `ng serve :4200` | `localhost:4200` | `http://localhost:4200` |
| Docker Compose | WebApi `wwwroot` | `localhost:5001` | `http://localhost:5001` |
| Kubernetes | WebApi `wwwroot` | `https://YOUR_PUBLIC_DOMAIN` | `https://YOUR_PUBLIC_DOMAIN` |

`YOUR_PUBLIC_DOMAIN` placeholders in `appsettings.Kubernetes.json` will be replaced by a K8s ConfigMap — do not hardcode them.

## K8s Service Names (namespace: `nagp-ecom`)

| Service | Internal DNS |
|---|---|
| WebApi | `nagp-ecom-api.nagp-ecom.svc.cluster.local` |
| Identity API | `nagp-ecom-identity-api.nagp-ecom.svc.cluster.local` |
| PostgreSQL | `postgres` (within namespace) |

## Docker / CI

- **Unified image**: `Dockerfile.webapi` at `src/` root — build context must be `src/` so both `api/` and `ui/` are accessible
- **Docker Compose**: run from `src/api/`; `api` service context is `..` (i.e. `src/`)
- **GitHub Actions**: workflow at `.github/workflows/docker-image.yml`; `build-and-test` job paths are relative to `src/` (the repo root)
- Images pushed to `docker.io/ankurmathur01nagarro/`; tags updated in Kustomize overlays by the `release` job

## Secrets

- **Google OAuth credentials** (`ExternalAuth:Google:ClientId`, `ExternalAuth:Google:ClientSecret`) are secrets — never commit them for non-development environments. `appsettings.Development.json` may keep them for local dev convenience.
- In K8s, inject via environment variables from an `ExternalSecret` (Azure Key Vault) — do not put real credentials in `appsettings.Kubernetes.json`.
- `IdentityApi:ClientSecret` in WebApi and `OpenIddict:Clients:EcomApi:ClientSecret` in Identity API must always match — both are sourced from `secret/ECOMAPI-CLIENT-SECRET` in Key Vault.

## Azure Key Vault Secrets Required

| Key Vault key | Consumed as |
|---|---|
| `secret/POSTGRES-USERNAME` | DB credential |
| `secret/POSTGRES-PASSWORD` | DB credential |
| `secret/ECOMAPI-CLIENT-ID` | OpenIddict client ID |
| `secret/ECOMAPI-CLIENT-SECRET` | OpenIddict client secret (same value in both WebApi and Identity API) |
