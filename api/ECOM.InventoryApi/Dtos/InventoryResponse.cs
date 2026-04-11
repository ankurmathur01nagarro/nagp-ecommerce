using ECOM.InventoryApi.Data.DataModels;

namespace ECOM.InventoryApi.Dtos;

public record InventoryResponse(
    int Id,
    int ProductId,
    string Sku,
    int Quantity,
    int Reserved,
    int Available,
    int LowStockThreshold,
    InventoryMetadata? Metadata,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
