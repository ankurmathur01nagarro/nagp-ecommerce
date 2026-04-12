namespace ECOM.WebApi.Infrastructure;

public interface IImageLookupService
{
    ValueTask<ImageRecord?> GetAsync(Guid id, CancellationToken ct = default);
}
