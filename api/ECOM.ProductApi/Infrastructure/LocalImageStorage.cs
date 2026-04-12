using Microsoft.Extensions.Options;

namespace ECOM.ProductApi.Infrastructure;

/// <summary>
/// Writes images to the local filesystem — used in local Kubernetes (PVC) and development.
/// On the local cluster the PVC is mounted at <see cref="StorageOptions.LocalRoot"/>;
/// imgproxy reads from the same path via a shared volume mount.
/// </summary>
public sealed class LocalImageStorage(IOptions<StorageOptions> opts) : IImageStorage
{
    // Resolved once so all guards use the same canonical base.
    private string Root => Path.GetFullPath(opts.Value.LocalRoot);

    public async Task<string> SaveAsync(IFormFile file, int productId, CancellationToken ct)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var objectKey = $"{productId}/{Guid.NewGuid():N}{ext}";
        var fullPath = ResolveSafe(objectKey);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var dest = File.Create(fullPath);
        await file.CopyToAsync(dest, ct);

        return objectKey;
    }

    public Task DeleteAsync(string objectKey, CancellationToken ct)
    {
        var fullPath = ResolveSafe(objectKey);
        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Resolves <paramref name="objectKey"/> relative to <see cref="Root"/> and throws if the
    /// result would escape the storage root (path traversal guard).
    /// </summary>
    private string ResolveSafe(string objectKey)
    {
        var root = Root;
        var full = Path.GetFullPath(Path.Combine(root, objectKey));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Object key resolves outside the storage root.");
        return full;
    }
}
