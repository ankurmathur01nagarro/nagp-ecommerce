using System.Net.Http.Json;
using ECOM.WebApi.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace ECOM.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController(IHttpClientFactory httpClientFactory) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var productClient = httpClientFactory.CreateClient("product-api");
        var inventoryClient = httpClientFactory.CreateClient("inventory-api");

        var productResponse = await productClient.GetAsync($"/api/products/{id}", ct);
        if (productResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
            return NotFound();
        productResponse.EnsureSuccessStatusCode();

        var product = await productResponse.Content.ReadFromJsonAsync<ProductApiItem>(ct);
        if (product is null)
            return NotFound();

        var skus = new[] { product.Sku };

        // Fetch stock and offers in parallel
        var (stock, offersBySku) = await FetchStockAndOffersAsync(inventoryClient, skus, ct);

        return Ok(ToItem(product, stock.GetValueOrDefault(product.Sku), offersBySku));
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] ProductSearchRequest request, CancellationToken ct)
    {
        var productClient = httpClientFactory.CreateClient("product-api");
        var inventoryClient = httpClientFactory.CreateClient("inventory-api");

        // Normalise pagination before forwarding — page ≥ 1, pageSize clamped to [1, 20]
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 20 ? 20 : request.PageSize;
        request = request with { Page = page, PageSize = pageSize };

        // 1. Delegate filtering + sorting to ProductApi
        var productResponse = await productClient.PostAsJsonAsync("/api/products/search", request, ct);
        productResponse.EnsureSuccessStatusCode();
        var productList = await productResponse.Content.ReadFromJsonAsync<ProductApiSearchResult>(ct);

        var emptyFacets = new ProductFacetsDto([], [], [], [], []);

        if (productList is null || productList.Items.Count == 0)
            return Ok(new ProductCatalogResponse([], 0, page, pageSize, emptyFacets));

        var skus = productList.Items.Select(p => p.Sku).ToArray();

        // 2. Fetch stock and offers in parallel — SKU is the cross-service identifier
        var (stockBySku, offersBySku) = await FetchStockAndOffersAsync(inventoryClient, skus, ct);

        // 3. Merge; apply inStock filter if requested
        var merged = productList.Items
            .Select(p => ToItem(p, stockBySku.GetValueOrDefault(p.Sku), offersBySku))
            .ToList();

        if (request.InStock == true)
            merged = merged.Where(p => p.InStock).ToList();
        else if (request.InStock == false)
            merged = merged.Where(p => !p.InStock).ToList();

        return Ok(new ProductCatalogResponse(merged, productList.TotalCount, page, pageSize, productList.Facets));
    }

    // -------------------------------------------------------------------------

    /// <summary>
    /// Calls /api/inventory/stock and /api/inventory/offers in parallel for the given SKUs.
    /// Returns stock keyed by SKU and a lookup of offer lists keyed by SKU.
    /// </summary>
    private static async Task<(
        Dictionary<string, ProductStockInfo> StockBySku,
        Dictionary<string, List<SkuOfferDetail>> OffersBySku)>
        FetchStockAndOffersAsync(HttpClient client, string[] skus, CancellationToken ct)
    {
        var body = new { skus };

        var stockTask  = client.PostAsJsonAsync("/api/inventory/stock",  body, ct);
        var offersTask = client.PostAsJsonAsync("/api/inventory/offers", body, ct);

        // Fire both requests concurrently; await individually so exceptions surface naturally
        var stockResponse  = await stockTask;
        var offersResponse = await offersTask;

        Dictionary<string, ProductStockInfo> stockBySku = [];
        if (stockResponse.IsSuccessStatusCode)
        {
            var bulk = await stockResponse.Content.ReadFromJsonAsync<BulkStockResponse>(ct);
            stockBySku = bulk?.Items.ToDictionary(s => s.Sku, StringComparer.OrdinalIgnoreCase) ?? [];
        }

        Dictionary<string, List<SkuOfferDetail>> offersBySku = [];
        if (offersResponse.IsSuccessStatusCode)
        {
            var offersResult = await offersResponse.Content.ReadFromJsonAsync<InventoryOffersResponse>(ct);
            offersBySku = offersResult?.Items
                .ToDictionary(o => o.Sku, o => o.Offers, StringComparer.OrdinalIgnoreCase) ?? [];
        }

        return (stockBySku, offersBySku);
    }

    private static ProductCatalogItem ToItem(
        ProductApiItem p,
        ProductStockInfo? stock,
        Dictionary<string, List<SkuOfferDetail>> offersBySku) => new(
            p.Id,
            p.Name,
            p.Sku,
            p.ShortDescription,
            p.Description,
            p.Price,
            p.CategoryId,
            p.CategoryName,
            p.BrandId,
            p.BrandName,
            p.Gender,
            p.Images,
            p.Metadata,
            p.CreatedAt,
            p.UpdatedAt,
            stock?.Available ?? 0,
            stock?.InStock ?? false,
            stock?.ActiveOffer,
            offersBySku.GetValueOrDefault(p.Sku) ?? []);
}
