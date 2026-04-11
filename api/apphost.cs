#:sdk Aspire.AppHost.Sdk@13.1.0
#:package Aspire.Hosting.AppHost
#:package Aspire.Hosting.PostgreSQL
#:package Aspire.Hosting.Azure.KeyVault

var builder = DistributedApplication.CreateBuilder(args);

var keyvault = builder.AddAzureKeyVault("kv-nagp-ecom-secrets");

// PostgreSQL with two databases (mirrors local k8s setup)
var postgres = builder.AddPostgres("postgres")
    .WithHostPort(5432)
    .WithImage("postgres", "18.3-alpine")
    .WithDataVolume("ecom-pgdata")
    .WithPgAdmin();

var ecomDb = postgres.AddDatabase("ecomdb");
var productDb = postgres.AddDatabase("productdb");
var inventoryDb = postgres.AddDatabase("inventorydb");

// Migration jobs run to completion before APIs start
var ecomMigrations = builder.AddProject("ecom-webapi-migrations", "ECOM.WebApi.MigrationJob/ECOM.WebApi.MigrationJob.csproj")
    .WithReference(ecomDb, "Default")
    .WaitFor(ecomDb);

var productMigrations = builder.AddProject("ecom-productapi-migrations", "ECOM.ProductApi.MigrationJob/ECOM.ProductApi.MigrationJob.csproj")
    .WithReference(productDb)
    .WaitFor(productDb);

var inventoryMigrations = builder.AddProject("ecom-inventoryapi-migrations", "ECOM.InventoryApi.MigrationJob/ECOM.InventoryApi.MigrationJob.csproj")
    .WithReference(inventoryDb)
    .WaitFor(inventoryDb);

// Identity API (OpenIddict authorization server)
var identityApi = builder.AddProject("ecom-identity-api", "ECOM.Identity.Api/ECOM.Identity.Api.csproj")
    .WithReference(ecomDb, "Default")
    .WithEnvironment("ExternalAuth__Google__ClientId", keyvault.GetSecret("GOOGLE-CLIENT-ID"))
    .WithEnvironment("ExternalAuth__Google__ClientSecret", keyvault.GetSecret("GOOGLE-CLIENT-SECRET"))
    .WithEnvironment("OpenIddict__Clients__EcomApi__ClientSecret", keyvault.GetSecret("ECOMAPI-CLIENT-SECRET"))
    .WaitForCompletion(ecomMigrations);

// Main Web API (depends on Identity for token issuance)
builder.AddProject("ecom-web-api", "ECOM.WebApi/ECOM.WebApi.csproj")
    .WithReference(ecomDb, "Default")
    .WithReference(identityApi)
    .WithEnvironment("IdentityApi__ClientSecret", keyvault.GetSecret("ECOMAPI-CLIENT-SECRET"))
    .WaitForCompletion(ecomMigrations)
    .WaitFor(identityApi);

// Product API
builder.AddProject("ecom-product-api", "ECOM.ProductApi/ECOM.ProductApi.csproj")
    .WithReference(productDb)
    .WaitForCompletion(productMigrations);

// Inventory API (stock, offers, user cart)
builder.AddProject("ecom-inventory-api", "ECOM.InventoryApi/ECOM.InventoryApi.csproj")
    .WithReference(inventoryDb)
    .WaitForCompletion(inventoryMigrations);

builder.Build().Run();
