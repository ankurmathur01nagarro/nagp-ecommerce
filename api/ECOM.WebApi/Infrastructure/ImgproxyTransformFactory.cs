using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace ECOM.WebApi.Infrastructure;

/// <summary>
/// YARP ITransformFactory for the imgproxy image lookup transform.
/// Routes opt in by adding { "ImgproxyLookup": "true" } to their Transforms array
/// in configuration — no RouteId is hardcoded here.
///
/// On each request the transform:
///   1. Parses the {id} route value as a GUID.
///   2. Resolves the source URL via IImageLookupService (HybridCache → ProductApi).
///   3. Rewrites the proxy path to /unsafe/plain/{encodedUrl}.
///   4. Forwards all client query parameters to imgproxy unchanged.
///   5. Stamps Cache-Control: public, max-age=86400, immutable on the response.
/// </summary>
internal sealed class ImgproxyTransformFactory : ITransformFactory
{
    private const string TransformKey = "ImgproxyLookup";

    public bool Validate(TransformRouteValidationContext context,
        IReadOnlyDictionary<string, string> transformValues)
    {
        if (!transformValues.TryGetValue(TransformKey, out _)) return false;
        return true; // no additional parameters to validate
    }

    public bool Build(TransformBuilderContext context,
        IReadOnlyDictionary<string, string> transformValues)
    {
        if (!transformValues.TryGetValue(TransformKey, out _)) return false;

        var lookup = context.Services.GetRequiredService<IImageLookupService>();

        context.AddRequestTransform(async ctx =>
        {
            var idStr = ctx.HttpContext.Request.RouteValues["id"]?.ToString();
            if (!Guid.TryParse(idStr, out var id))
            {
                ctx.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await ctx.HttpContext.Response.CompleteAsync();
                return;
            }

            var record = await lookup.GetAsync(id, ctx.HttpContext.RequestAborted);
            if (record is null)
            {
                ctx.HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                await ctx.HttpContext.Response.CompleteAsync();
                return;
            }

            var sourceBytes = System.Text.Encoding.UTF8.GetBytes(record.Url);
            var base64Url = System.Buffers.Text.Base64Url.EncodeToString(sourceBytes);
            ctx.Path = new PathString($"/unsafe/{base64Url}");
        });

        context.AddResponseTransform(ctx =>
        {
            ctx.HttpContext.Response.Headers.CacheControl = "public, max-age=86400, immutable";
            return ValueTask.CompletedTask;
        });

        return true;
    }
}
