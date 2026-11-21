using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UCR.EB.BioMicroscopeAdmin.Backend.Application.Security;
using UCR.EB.BioMicroscopeAdmin.Backend.Domain.Exceptions;
using UCR.EB.BioMicroscopeAdmin.Backend.Presentation.Dtos;

namespace UCR.EB.BioMicroscopeAdmin.Backend.Presentation.Endpoints;

public record CreateReaderRequest(string Email, string Password);

public record UserResponse(string Id, string Email, IReadOnlyList<string> Roles, bool IsActive);

/// <summary>Lets administrators manage reader accounts.</summary>
public static class UserEndpoints
{
    // Se distingue del bloqueo temporal por intentos fallidos, que sí expira.
    private static readonly DateTimeOffset DeactivatedUntil = DateTimeOffset.MaxValue;

    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder builder)
    {
        var users = builder.MapGroup("/users").RequireAuthorization(AuthPolicies.Administrator);
        users.MapGet(string.Empty, ListAsync);
        users.MapPost(string.Empty, CreateReaderAsync);
        users.MapPost("/{id}/deactivate", DeactivateAsync);
        users.MapPost("/{id}/reactivate", ReactivateAsync);
        return builder;
    }

    private static async Task<IResult> ListAsync([FromServices] UserManager<IdentityUser> users)
    {
        var admins = await users.GetUsersInRoleAsync(AuthRoles.Administrator);
        var readers = await users.GetUsersInRoleAsync(AuthRoles.Reader);
        var result = admins.Select(user => ToResponse(user, AuthRoles.Administrator))
            .Concat(readers.Select(user => ToResponse(user, AuthRoles.Reader)))
            .OrderBy(user => user.Email, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateReaderAsync(
        [FromBody] CreateReaderRequest request,
        [FromServices] UserManager<IdentityUser> users)
    {
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
        {
            return Error("Email", "A valid email is required.");
        }

        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var created = await users.CreateAsync(user, request.Password ?? string.Empty);
        if (!created.Succeeded)
        {
            return ToErrors(created);
        }

        var role = await users.AddToRoleAsync(user, AuthRoles.Reader);
        if (!role.Succeeded)
        {
            await users.DeleteAsync(user);
            return ToErrors(role);
        }

        return Results.Created($"/api/users/{user.Id}", ToResponse(user, AuthRoles.Reader));
    }

    private static async Task<IResult> DeactivateAsync(
        [FromRoute] string id,
        ClaimsPrincipal currentUser,
        [FromServices] UserManager<IdentityUser> users)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null)
        {
            return Results.NotFound();
        }

        if (users.GetUserId(currentUser) == user.Id || !await users.IsInRoleAsync(user, AuthRoles.Reader))
        {
            return Error("User", "Only reader accounts can be deactivated.");
        }

        await users.SetLockoutEnabledAsync(user, true);
        var result = await users.SetLockoutEndDateAsync(user, DeactivatedUntil);
        if (!result.Succeeded)
        {
            return ToErrors(result);
        }

        // Cambiar el sello invalida las sesiones que ya estaban abiertas.
        await users.UpdateSecurityStampAsync(user);
        return Results.Ok(ToResponse(user, AuthRoles.Reader));
    }

    private static async Task<IResult> ReactivateAsync(
        [FromRoute] string id,
        [FromServices] UserManager<IdentityUser> users)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null)
        {
            return Results.NotFound();
        }

        if (!await users.IsInRoleAsync(user, AuthRoles.Reader))
        {
            return Error("User", "Only reader accounts can be reactivated.");
        }

        var result = await users.SetLockoutEndDateAsync(user, null);
        if (!result.Succeeded)
        {
            return ToErrors(result);
        }

        await users.ResetAccessFailedCountAsync(user);
        return Results.Ok(ToResponse(user, AuthRoles.Reader));
    }

    private static UserResponse ToResponse(IdentityUser user, string role) =>
        new(user.Id, user.Email ?? string.Empty, [role], user.LockoutEnd != DeactivatedUntil);

    private static IResult Error(string field, string message) =>
        Results.BadRequest(new ErrorResponse([new ValidationError(field, message)]));

    private static IResult ToErrors(IdentityResult result) =>
        Results.BadRequest(new ErrorResponse(result.Errors
            .Select(error => new ValidationError(error.Code, error.Description))
            .ToList()));
}
