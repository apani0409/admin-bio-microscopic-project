using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using UCR.EB.BioMicroscopeAdmin.Frontend.Application.Services;

namespace UCR.EB.BioMicroscopeAdmin.Frontend.Presentation.Security;

/// <summary>Role names used by the UI. They must match the API.</summary>
public static class AppRoles
{
    public const string Administrator = "Administrador";
    public const string Reader = "Lector";
}

/// <summary>Keeps the UI authentication state in sync with the API session cookie.</summary>
public sealed class CookieAuthStateProvider : AuthenticationStateProvider, IDisposable
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly IAuthService _auth;
    private readonly AuthSessionNotifier _notifier;
    private Task<AuthenticationState>? _state;

    public CookieAuthStateProvider(IAuthService auth, AuthSessionNotifier notifier)
    {
        _auth = auth;
        _notifier = notifier;
        _notifier.SessionExpired += OnSessionExpired;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _state ??= LoadAsync();

    public async Task<bool> LoginAsync(string email, string password)
    {
        var user = await _auth.LoginAsync(email, password);
        if (user is null)
        {
            return false;
        }

        SetState(ToState(user));
        return true;
    }

    public async Task LogoutAsync()
    {
        try
        {
            await _auth.LogoutAsync();
        }
        finally
        {
            SetState(Anonymous);
        }
    }

    public void Dispose() => _notifier.SessionExpired -= OnSessionExpired;

    private async Task<AuthenticationState> LoadAsync()
    {
        try
        {
            var user = await _auth.GetCurrentUserAsync();
            return user is null ? Anonymous : ToState(user);
        }
        catch (HttpRequestException)
        {
            return Anonymous;
        }
    }

    private void OnSessionExpired() => SetState(Anonymous);

    private void SetState(AuthenticationState state)
    {
        _state = Task.FromResult(state);
        NotifyAuthenticationStateChanged(_state);
    }

    private static AuthenticationState ToState(CurrentUser user)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, user.Email), new(ClaimTypes.Email, user.Email) };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "cookie")));
    }
}

/// <summary>Prevents open redirects after login.</summary>
public static class ReturnUrl
{
    public static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !value.StartsWith('/')
            || value.StartsWith("//", StringComparison.Ordinal)
            || value.Contains('\\')
            || value.Any(char.IsControl))
        {
            return "/";
        }

        return value;
    }
}
