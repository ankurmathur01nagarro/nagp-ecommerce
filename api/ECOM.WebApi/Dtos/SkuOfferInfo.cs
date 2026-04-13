namespace ECOM.WebApi.Dtos;

/// <summary>Internal DTO — mirrors InventoryApi's SkuOfferInfo for deserialization.</summary>
public record SkuOfferInfo(string Sku, List<SkuOfferDetail> Offers);
