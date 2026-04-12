#:sdk Aspire.AppHost.Sdk@13.2.2
#:package Aspire.Hosting.AppHost@13.2.2
#:package Aspire.Hosting.PostgreSQL@13.2.2
#:package Aspire.Hosting.Javascript@13.2.2
#:package Aspire.Hosting.Yarp@13.2.2

var builder = DistributedApplication.CreateBuilder(args);
var p_postgresPassword = builder.AddParameter("POSTGRES-PASSWORD", true);

// Shared images directory — product-api writes here, imgproxy reads from here.
// In local dev this is a plain folder; in k8s both pods mount the same PVC.
var imagesPath = Path.Combine(builder.AppHostDirectory, ".local-images");
Directory.CreateDirectory(imagesPath);

var p_googleClientId = builder.AddParameter("GOOGLE-CLIENT-ID", "283920841631-er35ovfc4fsmapnoarrlv6i32b6fj9f9.apps.googleusercontent.com", false, true);
var p_googleClientSecret = builder.AddParameter("GOOGLE-CLIENT-SECRET", true);
var p_ecomApiClientSecret = builder.AddParameter("ECOMAPI-CLIENT-SECRET", true);

// PostgreSQL with two databases (mirrors local k8s setup)
var postgres = builder.AddPostgres("postgres")
    .WithHostPort(5432)
    .WithImage("postgres", "18.3-alpine")
    .WithDataVolume("ecom-pgdata")
    .WithPassword(p_postgresPassword)
    .WithPgAdmin();

var ecomDb = postgres.AddDatabase("ecomdb");
var productDb = postgres.AddDatabase("productdb");
var inventoryDb = postgres.AddDatabase("inventorydb");

// Migration jobs run to completion before APIs start
var ecomMigrations = builder.AddProject("ecom-webapi-migrations", "api/ECOM.WebApi.MigrationJob/ECOM.WebApi.MigrationJob.csproj")
    .WithReference(ecomDb, "Default")
    .WaitFor(ecomDb);

var productMigrations = builder.AddProject("ecom-productapi-migrations", "api/ECOM.ProductApi.MigrationJob/ECOM.ProductApi.MigrationJob.csproj")
    .WithReference(productDb, "Default")
    .WaitFor(productDb);

var inventoryMigrations = builder.AddProject("ecom-inventoryapi-migrations", "api/ECOM.InventoryApi.MigrationJob/ECOM.InventoryApi.MigrationJob.csproj")
    .WithReference(inventoryDb, "Default")
    .WaitFor(inventoryDb);

// Identity API (OpenIddict authorization server)
var identityApi = builder.AddProject("ecom-identity-api", "api/ECOM.Identity.Api/ECOM.Identity.Api.csproj")
    .WithReference(ecomDb, "Default")
    .WithEnvironment("ExternalAuth__Google__ClientId", p_googleClientId)
    .WithEnvironment("ExternalAuth__Google__ClientSecret", p_googleClientSecret)
    .WithEnvironment("OpenIddict__Clients__EcomApi__ClientSecret", p_ecomApiClientSecret)
    .WaitForCompletion(ecomMigrations);

// var ui = builder.AddExternalService("ecom-web-app", "http://localhost:4200");
var ui = builder.AddJavaScriptApp("ecom-web-app", "ui")
    .WithHttpEndpoint(4200, env: "PORT")
    .WithNpm()
    .WithExternalHttpEndpoints();

// imgproxy — on-the-fly image resizing; reads from the shared images directory
var imgproxy = builder.AddContainer("ecom-imgproxy", "darthsim/imgproxy", "v3")
    .WithHttpEndpoint(9001, targetPort: 8080, name: "http")
    .WithBindMount(imagesPath, "/images", isReadOnly: true)
    .WithEnvironment("IMGPROXY_LOCAL_FILESYSTEM_ROOT", "/images")
    .WithEnvironment("IMGPROXY_ALLOWED_SOURCES", "local://,https://images.pexels.com/")
    .WithEnvironment("IMGPROXY_DEVELOPMENT_ERRORS_MODE", "true")
    .WithEnvironment("IMGPROXY_LOG_LEVEL", "debug");

// Product API (declared first so WebApi can reference it)
var productApi = builder.AddProject("ecom-product-api", "api/ECOM.ProductApi/ECOM.ProductApi.csproj")
    .WithReference(productDb, "Default")
    .WithEnvironment("Storage__LocalRoot", imagesPath)
    .WaitForCompletion(productMigrations);

// Main Web API (depends on Identity for token issuance)
builder.AddProject(
        "ecom-web-api",
        "api/ECOM.WebApi/ECOM.WebApi.csproj",
        "https")
    .WithReference(ecomDb, "Default")
    .WithReference(identityApi)
    .WithReference(productApi)
    .WithEnvironment("IdentityApi__ClientSecret", p_ecomApiClientSecret)
    .WithEnvironment("ReverseProxy__Clusters__ui__Destinations__primary__Address", ui.GetEndpoint("http"))
    .WithEnvironment("ReverseProxy__Clusters__identity__Destinations__primary__Address", identityApi.GetEndpoint("http"))
    .WithEnvironment("ImageProxy__BaseUrl", imgproxy.GetEndpoint("http"))
    .WithEnvironment("ProductApi__BaseUrl", productApi.GetEndpoint("http"))
    .WithDeveloperCertificateTrust(true)
    .WaitForCompletion(ecomMigrations)
    .WaitFor(identityApi)
    .WaitFor(imgproxy)
    .WaitFor(productApi);

// Inventory API (stock, offers, user cart)
builder.AddProject("ecom-inventory-api", "api/ECOM.InventoryApi/ECOM.InventoryApi.csproj")
    .WithReference(inventoryDb, "Default")
    .WaitForCompletion(inventoryMigrations);

builder.Build().Run();
