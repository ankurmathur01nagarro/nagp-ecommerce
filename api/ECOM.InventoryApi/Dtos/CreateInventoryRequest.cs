using ECOM.InventoryApi.Data.DataModels;

namespace ECOM.InventoryApi.Dtos;

public record CreateInventoryRequest(
    int ProductId,
    string Sku,
    int Quantity,
    int Reserved,
    int LowStockThreshold,
    InventoryMetadata? Metadata);
