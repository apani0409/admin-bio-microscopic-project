using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UCR.EB.BioMicroscopeAdmin.Backend.Application.Services;
using UCR.EB.BioMicroscopeAdmin.Backend.Presentation.Handlers.Images;
using UCR.EB.BioMicroscopeAdmin.Backend.Presentation.Security;

namespace UCR.EB.BioMicroscopeAdmin.Backend.Presentation.Endpoints;

/// <summary>Maps all endpoints related to images.</summary>
public static class ImageEndpoints
{
    public static IEndpointRouteBuilder MapImageEndpoints(this IEndpointRouteBuilder builder)
    {
        // El filtro CSRF del grupo /api protege esta subida.
        builder.MapPost("/slides/{id}/images", UploadImageHandler.HandleAsync)
            .DisableAntiforgery()
            .AddEndpointFilter<ImageUploadValidationFilter>();
        builder.MapGet("/slides/{id}/images", ListImagesBySlideHandler.HandleAsync);
        builder.MapGet("/images/{id:int}/file", ImageFileHandler.GetFileAsync);
        builder.MapGet("/images/{id:int}/thumbnail", ImageFileHandler.GetThumbnailAsync);
        builder.MapPut("/images/{id}", UpdateImageOrderHandler.HandleAsync);
        builder.MapDelete("/images/{id}", DeleteImageHandler.HandleAsync)
            .AddEndpointFilter(new DeleteImageFilesFilter(ImageOwner.Image));
        return builder;
    }
}
