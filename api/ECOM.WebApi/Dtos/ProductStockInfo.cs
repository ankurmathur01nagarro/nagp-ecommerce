namespace ECOM.WebApi.Dtos;

/// <summary>Internal DTO — mirrors InventoryApi's ProductStockSummary for deserialization.</summary>
public record ProductStockInfo(
    string Sku,
    int Available,
    bool InStock,
    ActiveOfferSummary? ActiveOffer);
