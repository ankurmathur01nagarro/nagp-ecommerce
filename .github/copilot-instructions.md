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
├── api/Dockerfile.webapi    ← Unified 3-stage image: ng-build → dotnet-build → aspnet runtime
└── .github/workflows/        ← GitHub Actions CI/CD
```

See `api/CLAUDE.md` for full build/run commands and architecture overview.

## Tech Stack

- **.NET 10**, **PostgreSQL 17**, **EF Core** (Npgsql provider)
- **OpenIddict 7.4.0** — Identity server (AddServer) + Google OAuth client (AddClient) in one process
- **YARP 2.3.0** — Embedded reverse proxy inside WebApi; routes browser-facing Identity endpoints
- **Angular 21** — SPA served from WebApi's `wwwroot` via `UseStaticFiles` + `MapFallbackToFile`
- **Gateway API HTTPRoute** (`gateway.networking.k8s.io/v1`) — not nginx Ingress, Istio gateway class
- **Kustomize** — base/overlays pattern; ArgoCD GitOps delivery
- **External Secrets Operator** — reads from Azure Key Vault (`kv-nagp-ecom-secrets`); uses `ClusterSecretStore` (not namespace-scoped `SecretStore`) so ExternalSecrets in any namespace can reference it
- **cert-manager** — DuckDNS DNS-01 webhook issues Let's Encrypt certs; webhook deployed via ArgoCD pointing at GitHub repo directly (not Helm Pages)
- **Domain** — `nagp-ecom.duckdns.org` → `192.168.1.240` (MetalLB). All public-facing URLs use this domain.

## Key Conventions

### .NET
- **One type per file** — every `record`, DTO, and result type has its own `.cs` file
- **Centralized NuGet versions** — all versions in `Directory.Packages.props`; never specify versions in individual `.csproj` files
- **`KnownIPNetworks.Clear()`** (not `KnownNetworks`) — use this for ForwardedHeaders in .NET 10
- **`ASPNETCORE_ENVIRONMENT: Kubernetes`** — third appsettings tier; `appsettings.Kubernetes.json` loaded in K8s pods
- **Public domain** — `https://nagp-ecom.duckdns.org` (not `YOUR_PUBLIC_DOMAIN` placeholder — that comment in the old file is outdated)
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

The public domain is `nagp-ecom.duckdns.org`. This is already set in `appsettings.Kubernetes.json` and the identity-api ConfigMap.

## K8s Service Names (namespace: `nagp-ecom`)

| Service | Internal DNS |
|---|---|
| WebApi | `nagp-ecom-api.nagp-ecom.svc.cluster.local` |
| Identity API | `nagp-ecom-identity-api.nagp-ecom.svc.cluster.local` |
| Product API | `nagp-ecom-productapi.nagp-ecom.svc.cluster.local` (note: no hyphen before "api") |
| Inventory API | `nagp-ecom-inventoryapi.nagp-ecom.svc.cluster.local` |
| imgproxy | `nagp-ecom-imgproxy.nagp-ecom.svc.cluster.local` |
| PostgreSQL | `postgres` (within namespace) |

## Docker / CI

- **Unified image**: `api/Dockerfile.webapi` in `src/api/` — build context must be `src/` so both `api/` and `ui/` are accessible
- **Docker Compose**: run from `src/api/`; `api` service context is `..` (i.e. `src/`)
- **GitHub Actions**: workflow at `.github/workflows/docker-image.yml`; `build-and-test` job paths are relative to `src/` (the repo root)
- Images pushed to `docker.io/ankurmathur01nagarro/`; tags updated in Kustomize overlays by the `release` job

## Secrets

- **Google OAuth credentials** (`ExternalAuth:Google:ClientId`, `ExternalAuth:Google:ClientSecret`) are secrets — never commit them for non-development environments. `appsettings.Development.json` may keep them for local dev convenience.
- In K8s, inject via environment variables from an `ExternalSecret` (Azure Key Vault) — do not put real credentials in `appsettings.Kubernetes.json`.
- `IdentityApi:ClientSecret` in WebApi and `OpenIddict:Clients:EcomApi:ClientSecret` in Identity API must always match — both are sourced from `secret/ECOMAPI-CLIENT-SECRET` in Key Vault.

## Kubernetes / ArgoCD Patterns

### Namespace
- All app resources use `namespace: nagp-ecom`, set explicitly in each resource's `metadata` — never use Kustomize `namespace:` field injection
- Overlay patches must include `metadata.namespace` to match the base resource ID (Kustomize matches on group/kind/namespace/name)
- `cert-manager` namespace for the DuckDNS webhook and its ExternalSecret

### ArgoCD Sync Waves
| Wave | Apps |
|------|------|
| 1 | `cert-manager-webhook-duckdns` |
| 3 | `nagp-ecom-infrastructure` (PostgreSQL, ClusterSecretStore, Certificate, Gateway) |
| 4 | `nagp-ecom-api`, `nagp-ecom-identity-api` |
| 5 | Observability (loki, tempo, prometheus, grafana, alloy) |

### ArgoCD Project Whitelists (cluster-scoped resources)
Cluster-scoped resources require explicit `clusterResourceWhitelist` entries — ArgoCD rejects syncs otherwise.
- `nagp-ecom` project: `ClusterIssuer` (cert-manager.io), `ClusterSecretStore` (external-secrets.io), `ClusterRole` + `ClusterRoleBinding` (rbac.authorization.k8s.io)
- `cert-manager` project: above + `CustomResourceDefinition` (apiextensions.k8s.io), `APIService` (apiregistration.k8s.io)

### cert-manager / TLS
- `ClusterIssuer: cert-manager-webhook-duckdns-production` uses DuckDNS DNS-01 challenge
- `Certificate: ecom-tls` in `nagp-ecom` → issues `ecom-tls-secret` used by the Gateway HTTPS listener
- DuckDNS token sourced from AKV → `duckdns-credentials` Secret in `cert-manager` via ExternalSecret
- Values file for the webhook chart: `deployment/scripts/deployment/cert-manager/values.cert-manager.yaml`

## Azure Key Vault Secrets Required

| Key Vault key | Consumed as |
|---|---|
| `secret/POSTGRES-USERNAME` | DB credential |
| `secret/POSTGRES-PASSWORD` | DB credential |
| `secret/ECOMAPI-CLIENT-ID` | OpenIddict client ID |
| `secret/ECOMAPI-CLIENT-SECRET` | OpenIddict client secret (same value in both WebApi and Identity API) |
| `secret/DUCKDNS-TOKEN` | cert-manager DuckDNS webhook token |

---

## Image Service Architecture (GUID-based indirection)
- Every `ProductImage` in the `Products.Images` JSONB has a stable `Guid Id` field (added by `AddImageIds` migration)
- Migration `AddImageIds` is data-only (no schema change); uses `gen_random_uuid()` + idempotent CASE WHEN
- Client calls `GET /images/{guid}?{any imgproxy query params}` on **WebApi** — all query params forwarded to imgproxy unchanged, server has no knowledge of them
- YARP handles forwarding entirely; no `IHttpForwarder` call in application code, no `"imgproxy"` named HttpClient
- WebApi resolves GUID → source URL via **HybridCache** (L1 in-process, 1-hour TTL) → ProductApi `GET /api/images/{guid}`
- YARP rewrites path to `/unsafe/plain/{encodedUrl}` and forwards raw query string to imgproxy
- imgproxy URL requires `unsafe` token when `IMGPROXY_KEY`/`IMGPROXY_SALT` are not set (dev mode)
- JSONB shape: `{ "id": "uuid", "url": "...", "alt": "...", "sortOrder": N }`
- ProductApi `ProductsController` calls `AssignMissingImageIds()` before serialising Images to JSONB (Create + Update)
- ImageCatalogRepository uses `jsonb_array_elements` unnest via Dapper — full-table scan acceptable because result always served from cache

## Image Service Components
- `ECOM.ProductApi.Data/Repositories/IImageCatalogRepository.cs` + `ImageCatalogRepository.cs`
- `ECOM.ProductApi/Controllers/ImagesController.cs` — `GET /api/images/{id:guid}` returns `{ id, url, alt }`
- `ECOM.WebApi/Infrastructure/ImageRecord.cs` — `[ImmutableObject(true)]` sealed record (required for HybridCache zero-copy)
- `ECOM.WebApi/Infrastructure/IImageLookupService.cs` + `ImageLookupService.cs` — HybridCache wrapper
- `ECOM.WebApi/Infrastructure/ImgproxyTransformFactory.cs` — `ITransformFactory`; routes opt in via `{ "ImgproxyLookup": "true" }` in config Transforms array; resolves GUID, rewrites path, stamps Cache-Control
- `ECOM.WebApi/Program.cs` — `AddHybridCache()`, `AddHttpClient("product-api")`, `.AddTransformFactory<ImgproxyTransformFactory>()` chained on `AddReverseProxy()`
- imgproxy destination configured solely in `ReverseProxy.Clusters.imgproxy` — no `ImageProxy:BaseUrl` config key

## HybridCache Package
- `Microsoft.Extensions.Caching.Hybrid` version `10.4.0` is the .NET 10-era NuGet package — must be referenced explicitly (NOT in shared framework)
- `GetOrCreateAsync` signature: `(key, state, static async (state, ct) => ...)` — state is passed as second positional arg, factory as third

## Categories API
- `GET /api/categories` (WebApi) — public endpoint, no auth, returns recursive `Category[]` (id: int, name, subcategories)
- WebApi `CategoriesController` injects `IHttpClientFactory`, creates `"product-api"` client, calls `GET /api/categories`, deserialises `List<CategoryResponse>`, returns `Ok(result)`
- ProductApi `CategoriesController` injects `ICategoryRepository`, fetches flat rows, builds tree in O(n): `ToDictionary` → single loop, roots = items where `ParentCategoryId IS NULL`
- `CategoryRepository` uses `NpgsqlDataSource` (Dapper): `SELECT "Id", "Name", "ParentCategoryId" FROM "ProductCategories" ORDER BY "Id"`
- `FlatCategory` is an internal repository-layer record — kept in its own file (`FlatCategory.cs`) per one-model-per-file rule; NOT the same as `CategoryResponse` DTO

## imgproxy Kubernetes
- ConfigMap: `IMGPROXY_OPEN_TELEMETRY_ENABLE: "true"`, `IMGPROXY_OPEN_TELEMETRY_ENABLE_METRICS: "true"` (not `IMGPROXY_ENABLE_OPEN_TELEMETRY`)
- Deployment and Service in `deployment/scripts/deployment/imgproxy/base/`

## Aspire AppHost
- imgproxy: `AddContainer` with bind mount to `.local-images`, `IMGPROXY_DEVELOPMENT_ERRORS_MODE=true`, `IMGPROXY_LOG_LEVEL=debug`
- `productApi` variable declared **before** WebApi builder (WebApi references it via `.WithReference(productApi)`)
- WebApi env vars: `ImageProxy__BaseUrl` from `imgproxy.GetEndpoint("http")`, `ProductApi__BaseUrl` from `productApi.GetEndpoint("http")`

---

## EF Core Migration Lessons

### Required files per migration
EF needs 3 files — missing any causes silent skip or runtime errors:
1. `{timestamp}_{Name}.cs` — Up/Down methods
2. `{timestamp}_{Name}.Designer.cs` — `[Migration("id")]` + `[DbContext]` attributes; EF discovers migrations from this
3. `{DbContext}ModelSnapshot.cs` — must match current model or `PendingModelChangesWarning` throws at `MigrateAsync`

### Hand-written migrations will be skipped
Without the Designer.cs, EF does not discover the migration at all — no error, silent skip.
Always use `dotnet ef migrations add` to generate all 3 files.

### Hand-written Designer.cs workaround
If the migration body (.cs) must be kept (e.g. seed data), copy the Designer.cs from the previous migration and:
- Change `[Migration("old_id")]` to the new timestamp+name
- Change `partial class OldName` to `partial class NewName`
- The `BuildTargetModel` body is identical if it's a data-only migration (no schema changes)

### Startup project needs Microsoft.EntityFrameworkCore.Design
Add to the startup project's `.csproj`:
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Design">
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
```

### Migration ordering
EF applies migrations in lexicographic order of the timestamp prefix (`yyyyMMddHHmmss`).
If a hand-written migration has an earlier timestamp than a schema migration it depends on, rename the file.
EF derives the migration ID from the filename — class internals don't need to change for a rename.

### Diagnosing pending model changes
Run `dotnet ef migrations add DiagnosticCheck --no-connect` (if supported) or with a live DB.
If the generated Up/Down is empty → model is in sync.
Delete the diagnostic migration files manually if DB unavailable (`ef migrations remove` needs a live DB).

### Unknown migrations in __EFMigrationsHistory
If a migration ID is in the DB history but not in the assembly, EF silently skips it — not an error.
