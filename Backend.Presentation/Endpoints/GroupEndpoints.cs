using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UCR.EB.BioMicroscopeAdmin.Backend.Presentation.Handlers.Groups;

namespace UCR.EB.BioMicroscopeAdmin.Backend.Presentation.Endpoints;

/// <summary>Maps all endpoints related to biological groups.</summary>
public static class GroupEndpoints
{
    public static IEndpointRouteBuilder MapGroupEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("/groups", CreateGroupHandler.HandleAsync);
        builder.MapGet("/groups", ListGroupsHandler.HandleAsync);
        builder.MapGet("/groups/{id}", GetGroupHandler.HandleAsync);
        builder.MapPut("/groups/{id}", UpdateGroupHandler.HandleAsync);
        builder.MapDelete("/groups/{id}", DeleteGroupHandler.HandleAsync)
            .AddEndpointFilter(new Security.DeleteImageFilesFilter(Application.Services.ImageOwner.Group));
        return builder;
    }
}
