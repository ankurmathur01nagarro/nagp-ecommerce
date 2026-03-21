# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
# Restore dependencies
dotnet restore

# Build entire solution
dotnet build

# Run the main Web API (http://localhost:5170)
dotnet run --project ECOM.WebApi/ECOM.WebApi.csproj

# Run the Identity/Auth API (http://localhost:5224)
dotnet run --project ECOM.Identity.Api/ECOM.Identity.Api.csproj

# Run database migrations
dotnet run --project ECOM.WebApi.MigrationJob/ECOM.WebApi.MigrationJob.csproj

# Add EF Core migration
dotnet ef migrations add <MigrationName> --project ECOM.WebApi.Data --startup-project ECOM.WebApi

# Apply migrations via EF CLI (alternative to MigrationJob)
dotnet ef database update --project ECOM.WebApi.Data --startup-project ECOM.WebApi
```

## Architecture Overview

This is an e-commerce backend built on **.NET 10** with **PostgreSQL 17** as the database.

### Projects

| Project | Type | Purpose |
|---|---|---|
| `ECOM.WebApi` | ASP.NET Web API | Main product/cart API |
| `ECOM.Identity.Api` | ASP.NET Web API | Auth service using OpenIddict |
| `ECOM.WebApi.Data` | Class Library | Shared EF Core models, DbContext, migrations |
| `ECOM.WebApi.MigrationJob` | Console App | Runs `MigrateAsync()` on startup; used as a Kubernetes init container |

`ECOM.WebApi` and `ECOM.WebApi.MigrationJob` both reference `ECOM.WebApi.Data`. `ECOM.Identity.Api` has its own direct Npgsql/OpenIddict setup.

### Key Patterns

- **Centralized package versions** — All NuGet versions are in [Directory.Packages.props](Directory.Packages.props). Do not specify versions in individual `.csproj` files.
- **API docs** — OpenAPI served via Scalar at `/scalar` (dev-only). The `.http` files in each project are for VS REST client testing.
- **Database extensions** — `ECOM.WebApi` registers the DbContext via a `RegisterDatabaseServices()` extension method (in `ECOM.WebApi.Data`).
- **JSONB** — `Products.Metadata` is a JSONB column in PostgreSQL.

### Data Model

Core entities: `Users`, `Products`, `ProductCategory` (self-referencing hierarchy), `ProductImages`, `ProductInventory`, `UserProductCart`. See [ECOM.WebApi.Data/ecom_database.dbml](ECOM.WebApi.Data/ecom_database.dbml) for the schema diagram.

### Deployment

Kubernetes manifests use **Kustomize** with two overlays:
- `deployment/overlays/local/` — local cluster, uses local PostgreSQL patch
- `deployment/overlays/cloud/` — cloud environment, uses cloud PVC/StatefulSet patches

The PostgreSQL StatefulSet runs `postgres:18.3-alpine` in namespace `nagp-ecom`.

### Known Issues

- `Dockerfile.api` and `Dockerfile.migration` still reference the old project names (`NAGP.Api`, `NAGP.Api.MigrationJob`) and need to be updated to `ECOM.WebApi` and `ECOM.WebApi.MigrationJob`.
- The `Migrator.cs` file has a variable named `movieDbContext` (copy-paste artifact) but functions correctly.
- Local database connection string (`Host=localhost;port=5432;Database=ecomdb;Username=postgres;Password=password`) is hardcoded in `ECOM.WebApi.MigrationJob/appsettings.json`; override via environment variable or `appsettings.Development.json` for local dev.
