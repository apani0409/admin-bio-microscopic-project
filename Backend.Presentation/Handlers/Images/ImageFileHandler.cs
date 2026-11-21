using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using UCR.EB.BioMicroscopeAdmin.Backend.Application.Services;

namespace UCR.EB.BioMicroscopeAdmin.Backend.Presentation.Handlers.Images;

/// <summary>Serves protected image files to authorized users.</summary>
public static class ImageFileHandler
{
    public static async Task<IResult> GetFileAsync(
        [FromRoute] int id,
        HttpContext http,
        [FromServices] IImageService images,
        [FromServices] IFileStorageService storage)
    {
        var image = await images.FindByIdAsync(id);
        var file = image is null ? null : await storage.OpenImageAsync(image.FileUrl.Value);
        return ToResult(http, file, $"\"image-{id}\"");
    }

    public static async Task<IResult> GetThumbnailAsync(
        [FromRoute] int id,
        HttpContext http,
        [FromServices] IImageService images,
        [FromServices] IFileStorageService storage)
    {
        var image = await images.FindByIdAsync(id);
        var file = image is null ? null : await storage.OpenThumbnailAsync(image.FileUrl.Value);
        return ToResult(http, file, $"\"thumbnail-{id}\"");
    }

    private static IResult ToResult(HttpContext http, StoredFile? file, string entityTag)
    {
        if (file is null)
        {
            return Results.NotFound();
        }

        // El navegador debe revalidar cada vez, para que cerrar sesión corte el acceso.
        http.Response.Headers.CacheControl = "private, no-cache";
        http.Response.Headers.XContentTypeOptions = "nosniff";
        http.Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        return Results.File(file.Content, file.ContentType, entityTag: new EntityTagHeaderValue(entityTag));
    }
}
