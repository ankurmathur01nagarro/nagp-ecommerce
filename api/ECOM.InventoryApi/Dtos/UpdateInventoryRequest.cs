using ECOM.InventoryApi.Data.DataModels;

namespace ECOM.InventoryApi.Dtos;

public record UpdateInventoryRequest(
    string Sku,
    int Quantity,
    int Reserved,
    int LowStockThreshold,
    InventoryMetadata? Metadata);
