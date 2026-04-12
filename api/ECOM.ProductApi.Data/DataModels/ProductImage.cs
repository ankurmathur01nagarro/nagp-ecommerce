namespace ECOM.ProductApi.Data.DataModels;

public class ProductImage
{
    public string Id { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Alt { get; set; }
    public int SortOrder { get; set; }
}
