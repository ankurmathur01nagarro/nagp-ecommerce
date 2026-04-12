using ECOM.ProductApi.Data.Repositories;
using ECOM.ProductApi.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace ECOM.ProductApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImagesController(IImageStorage storage, IImageCatalogRepository catalog) : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    /// <summary>
    /// Resolve an image by its stable GUID.
    /// Returns { id, url, alt } — consumed by ECOM.WebApi's image-proxy endpoint
    /// which caches the result in HybridCache before forwarding to imgproxy.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var image = await catalog.GetByIdAsync(id, ct);
        if (image is null)
            return NotFound();

        return Ok(image);
    }

    /// <summary>
    /// Upload a product image.
    /// Returns the object key which is stored in the product's Images JSONB alongside
    /// the stable GUID assigned by the server. The frontend only ever references images
    /// by GUID via the WebApi image-proxy: GET /images/{guid}
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromQuery] int productId,
        CancellationToken ct)
    {
        if (file.Length == 0)
            return BadRequest("File is empty.");

        if (file.Length > MaxFileSizeBytes)
            return BadRequest($"File exceeds the {MaxFileSizeBytes / 1024 / 1024} MB limit.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest($"Only {string.Join(", ", AllowedExtensions)} files are accepted.");

        var objectKey = await storage.SaveAsync(file, productId, ct);
        return Ok(new { objectKey });
    }

    /// <summary>
    /// Delete a product image.
    /// Accepts the objectKey returned by Upload (e.g. "42/a1b2c3d4e5f6.jpg").
    /// </summary>
    [HttpDelete("{**objectKey}")]
    public async Task<IActionResult> Delete(string objectKey, CancellationToken ct)
    {
        await storage.DeleteAsync(objectKey, ct);
        return NoContent();
    }
}
