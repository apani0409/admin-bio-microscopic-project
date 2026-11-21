namespace UCR.EB.BioMicroscopeAdmin.Frontend.Application.Services;

/// <summary>Authenticated user returned by the API.</summary>
public sealed record CurrentUser(string Email, IReadOnlyList<string> Roles);

/// <summary>Session operations against the API.</summary>
public interface IAuthService
{
    /// <summary>Returns the user, or null when the credentials are rejected.</summary>
    Task<CurrentUser?> LoginAsync(string email, string password);

    Task LogoutAsync();

    /// <summary>Returns the current user, or null when there is no active session.</summary>
    Task<CurrentUser?> GetCurrentUserAsync();
}

/// <summary>Lets the HTTP layer report an expired session to the UI.</summary>
public sealed class AuthSessionNotifier
{
    public event Action? SessionExpired;

    public void NotifySessionExpired() => SessionExpired?.Invoke();
}
