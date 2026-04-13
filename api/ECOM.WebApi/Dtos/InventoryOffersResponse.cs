namespace ECOM.WebApi.Dtos;

/// <summary>Internal DTO — mirrors InventoryApi's OffersBySkuResponse for deserialization.</summary>
public record InventoryOffersResponse(List<SkuOfferInfo> Items);
