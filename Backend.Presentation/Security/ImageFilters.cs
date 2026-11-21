using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UCR.EB.BioMicroscopeAdmin.Backend.Application.Services;
using UCR.EB.BioMicroscopeAdmin.Backend.Domain.Exceptions;
using UCR.EB.BioMicroscopeAdmin.Backend.Domain.ValueObjects;
using UCR.EB.BioMicroscopeAdmin.Backend.Presentation.Dtos;

namespace UCR.EB.BioMicroscopeAdmin.Backend.Presentation.Security;

/// <summary>Validates content, size, image count and position before storing an upload.</summary>
public sealed class ImageUploadValidationFilter : IEndpointFilter
{
    private const int DefaultMaxUploadMb = 10;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var file = context.Arguments.OfType<IFormFile>().FirstOrDefault();
        if (file is null || file.Length == 0 || !int.TryParse(http.Request.RouteValues["id"]?.ToString(), out var slideId))
        {
            return await next(context);
        }

        var maxMb = http.RequestServices.GetRequiredService<IConfiguration>()
            .GetValue("Storage:MaxUploadMb", DefaultMaxUploadMb);
        if (file.Length > maxMb * 1024L * 1024L)
        {
            return Reject($"The file cannot exceed {maxMb} MB.");
        }

        var storage = http.RequestServices.GetRequiredService<IFileStorageService>();
        await using (var stream = file.OpenReadStream())
        {
            if (storage.ValidateImage(stream) is { } error)
            {
                return Reject(error);
            }
        }

        var images = await http.RequestServices.GetRequiredService<IImageService>().FindBySlideAsync(slideId);
        if (images.Count >= ImageOrder.Max)
        {
            return Reject($"A slide can have at most {ImageOrder.Max} images.");
        }

        var order = context.Arguments.OfType<int>().Skip(1).FirstOrDefault();
        if (images.Any(image => image.Order.Value == order))
        {
            return Reject($"Position {order} is already in use.");
        }

        return await next(context);
    }

    private static IResult Reject(string message) =>
        Results.BadRequest(new ErrorResponse([new ValidationError("File", message)]));
}

/// <summary>Removes stored files after their entity, and its cascade, was deleted.</summary>
public sealed class DeleteImageFilesFilter(ImageOwner owner) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        if (!int.TryParse(context.HttpContext.Request.RouteValues["id"]?.ToString(), out var id))
        {
            return await next(context);
        }

        var files = await services.GetRequiredService<IImageFileLookup>().FindFileUrlsAsync(owner, id);
        var result = await next(context);

        var effective = result is INestedHttpResult nested ? nested.Result : result;
        if (effective is not IStatusCodeHttpResult { StatusCode: StatusCodes.Status204NoContent })
        {
            return result;
        }

        var storage = services.GetRequiredService<IFileStorageService>();
        foreach (var file in files)
        {
            try
            {
                await storage.DeleteAsync(file);
            }
            catch (IOException ex)
            {
                // El registro ya se borró: un archivo bloqueado no debe convertirlo en error.
                services.GetRequiredService<ILoggerFactory>().CreateLogger<DeleteImageFilesFilter>()
                    .LogError(ex, "Could not delete stored file {File}", file);
            }
        }

        return result;
    }
}
