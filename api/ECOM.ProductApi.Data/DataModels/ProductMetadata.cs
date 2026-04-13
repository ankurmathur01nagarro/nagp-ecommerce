namespace ECOM.ProductApi.Data.DataModels;

public class ProductMetadata
{
    public List<ProductColor> Colors { get; set; } = [];
    public List<string> Sizes { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public List<TechSpec> TechSpecs { get; set; } = [];
    public int? Rating { get; set; }
    public string? AdditionalInfo { get; set; }
}
