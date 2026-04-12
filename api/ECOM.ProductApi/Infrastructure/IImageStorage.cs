namespace ECOM.ProductApi.Infrastructure;

public interface IImageStorage
{
    /// <summary>Saves the uploaded file and returns the object key (relative path).</summary>
    Task<string> SaveAsync(IFormFile file, int productId, CancellationToken ct);

    Task DeleteAsync(string objectKey, CancellationToken ct);
}
