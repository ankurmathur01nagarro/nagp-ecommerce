namespace ECOM.ProductApi.Infrastructure;

public class StorageOptions
{
    public const string Section = "Storage";

    /// <summary>Absolute path where images are written. Mount a PVC here in both local and cloud clusters.</summary>
    public string LocalRoot { get; set; } = "/app/images";
}
