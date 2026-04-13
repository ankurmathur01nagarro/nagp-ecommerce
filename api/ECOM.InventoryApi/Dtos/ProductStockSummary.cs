namespace ECOM.InventoryApi.Dtos;

public record ProductStockSummary(
    string Sku,
    int Available,
    bool InStock,
    ActiveOfferSummary? ActiveOffer);
