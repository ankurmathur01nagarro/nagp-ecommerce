using ECOM.ProductApi.Data.DataModels;

namespace ECOM.ProductApi.Data.Repositories;

public interface IImageCatalogRepository
{
    /// <summary>
    /// Returns the <see cref="ProductImage"/> entry whose <c>id</c> JSONB field matches
    /// <paramref name="id"/>, or <c>null</c> if no product contains that image.
    /// </summary>
    Task<ProductImage?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
