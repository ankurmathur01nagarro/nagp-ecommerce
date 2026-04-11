namespace ECOM.InventoryApi.Data.DataModels;

public class OfferRules
{
    public int? MinQuantity { get; set; }
    public int? MaxQuantity { get; set; }
    public List<int> ApplicableCategoryIds { get; set; } = [];
    public List<int> ApplicableBrandIds { get; set; } = [];
    public List<string> CouponCodes { get; set; } = [];
    public List<string> Tags { get; set; } = [];
}
