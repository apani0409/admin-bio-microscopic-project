using System.Net;
using System.Net.Http.Json;
using UCR.EB.BioMicroscopeAdmin.Frontend.Application.Services;

namespace UCR.EB.BioMicroscopeAdmin.Frontend.Infrastructure.Auth;

internal sealed class AuthService(HttpClient http) : IAuthService
{
    public async Task<CurrentUser?> LoginAsync(string email, string password)
    {
        var response = await http.PostAsJsonAsync("auth/login", new { email, password });
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CurrentUser>();
    }

    public async Task LogoutAsync()
    {
        var response = await http.PostAsync("auth/logout", null);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            response.EnsureSuccessStatusCode();
        }
    }

    public async Task<CurrentUser?> GetCurrentUserAsync()
    {
        var response = await http.GetAsync("auth/me");
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CurrentUser>();
    }
}
