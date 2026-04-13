using System.Net.Http.Json;
using System.Security.Claims;
using ECOM.WebApi.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Validation.AspNetCore;

namespace ECOM.WebApi.Controllers;

[ApiController]
[Route("api/cart")]
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
public class CartController(IHttpClientFactory httpClientFactory) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var productClient = httpClientFactory.CreateClient("product-api");
        var inventoryClient = httpClientFactory.CreateClient("inventory-api");

        // 1. Fetch product details from ProductApi
        var productResponse = await productClient.GetAsync($"/api/products/{request.ProductId}", ct);
        if (productResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
            return NotFound(new { error = "Product not found." });
        productResponse.EnsureSuccessStatusCode();

        var product = await productResponse.Content.ReadFromJsonAsync<ProductApiItem>(ct);
        if (product is null)
            return NotFound(new { error = "Product not found." });

        // 2. Fetch active offer for this SKU from InventoryApi
        var stockBody = new { skus = new[] { product.Sku } };
        var stockResponse = await inventoryClient.PostAsJsonAsync("/api/inventory/stock", stockBody, ct);

        ActiveOfferSummary? activeOffer = null;
        if (stockResponse.IsSuccessStatusCode)
        {
            var bulk = await stockResponse.Content.ReadFromJsonAsync<BulkStockResponse>(ct);
            activeOffer = bulk?.Items
                .FirstOrDefault(s => string.Equals(s.Sku, product.Sku, StringComparison.OrdinalIgnoreCase))
                ?.ActiveOffer;
        }

        // 3. Add the item to the user's cart via InventoryApi
        var addItemPayload = new
        {
            productId = request.ProductId,
            sku = product.Sku,
            name = product.Name,
            unitPrice = product.Price,
            quantity = 1,
            imageUrl = product.Images?.FirstOrDefault()?.Url,
            appliedOfferId = (int?)null
        };

        var cartResponse = await inventoryClient.PostAsJsonAsync(
            $"/api/carts/{userId}/items", addItemPayload, ct);
        cartResponse.EnsureSuccessStatusCode();

        var cart = await cartResponse.Content.ReadFromJsonAsync<InventoryCartResponse>(ct);
        if (cart is null)
            return StatusCode(StatusCodes.Status500InternalServerError);

        return Ok(ToCartDetails(cart, request.ProductId, activeOffer));
    }

    [HttpDelete("items/{productId:int}")]
    public async Task<IActionResult> RemoveItem(int productId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var inventoryClient = httpClientFactory.CreateClient("inventory-api");

        var response = await inventoryClient.DeleteAsync($"/api/carts/{userId}/items/{productId}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return NotFound();
        response.EnsureSuccessStatusCode();

        var cart = await response.Content.ReadFromJsonAsync<InventoryCartResponse>(ct);
        if (cart is null)
            return StatusCode(StatusCodes.Status500InternalServerError);

        return Ok(ToCartDetails(cart));
    }

    [HttpDelete]
    public async Task<IActionResult> ClearCart(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var inventoryClient = httpClientFactory.CreateClient("inventory-api");

        var response = await inventoryClient.PostAsync($"/api/carts/{userId}/clear", null, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return NotFound();
        response.EnsureSuccessStatusCode();

        var cart = await response.Content.ReadFromJsonAsync<InventoryCartResponse>(ct);
        if (cart is null)
            return StatusCode(StatusCodes.Status500InternalServerError);

        return Ok(ToCartDetails(cart));
    }

    // -------------------------------------------------------------------------

    private bool TryGetUserId(out int userId)
    {
        var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.FindFirst("sub")?.Value;
        return int.TryParse(raw, out userId);
    }

    private static CartDetails ToCartDetails(
        InventoryCartResponse cart, int newProductId, ActiveOfferSummary? offer)
    {
        var items = cart.Items.Select(i =>
        {
            var itemOffer = i.ProductId == newProductId ? offer : null;
            return new CartDetailsItem(i.ProductId.ToString(), i.Quantity, i.UnitPrice, itemOffer);
        }).ToList();

        return new CartDetails(items, items.Sum(i => i.Price * i.Quantity));
    }

    private static CartDetails ToCartDetails(InventoryCartResponse cart)
    {
        var items = cart.Items
            .Select(i => new CartDetailsItem(i.ProductId.ToString(), i.Quantity, i.UnitPrice, null))
            .ToList();

        return new CartDetails(items, items.Sum(i => i.Price * i.Quantity));
    }
}
