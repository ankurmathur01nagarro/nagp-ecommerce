namespace ECOM.WebApi.Dtos;

/// <summary>Internal DTO — mirrors InventoryApi's BulkStockResponse for deserialization.</summary>
public record BulkStockResponse(List<ProductStockInfo> Items);
