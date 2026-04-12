namespace ECOM.ProductApi.Data.DataModels;

public class ProductImage
{
    public Guid Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Alt { get; set; }
    public int SortOrder { get; set; }
}
