using Microsoft.Extensions.Caching.Hybrid;
using System.Net.Http.Json;

namespace ECOM.WebApi.Infrastructure;

public sealed class ImageLookupService(HybridCache cache, IHttpClientFactory factory) : IImageLookupService
{
    public ValueTask<ImageRecord?> GetAsync(Guid id, CancellationToken ct = default) =>
        cache.GetOrCreateAsync(
            $"img:{id}",
            (factory, id),
            static async (state, ct) =>
            {
                var (httpFactory, imageId) = state;
                var client = httpFactory.CreateClient("product-api");
                var response = await client.GetAsync($"/api/images/{imageId}", ct);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ImageRecord>(ct);
            },
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromHours(1) },
            cancellationToken: ct);
}
