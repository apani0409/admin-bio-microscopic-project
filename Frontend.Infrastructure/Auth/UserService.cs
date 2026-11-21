using System.Net;
using System.Net.Http.Json;
using UCR.EB.BioMicroscopeAdmin.Frontend.Application.Services;

namespace UCR.EB.BioMicroscopeAdmin.Frontend.Infrastructure.Auth;

internal sealed class UserService(HttpClient http) : IUserService
{
    public async Task<List<UserDto>> ListAsync() =>
        await http.GetFromJsonAsync<List<UserDto>>("users") ?? [];

    public async Task CreateReaderAsync(string email, string password) =>
        await EnsureSuccessAsync(await http.PostAsJsonAsync("users", new { email, password }));

    public async Task DeactivateAsync(string id) =>
        await EnsureSuccessAsync(await http.PostAsync($"users/{Uri.EscapeDataString(id)}/deactivate", null));

    public async Task ReactivateAsync(string id) =>
        await EnsureSuccessAsync(await http.PostAsync($"users/{Uri.EscapeDataString(id)}/reactivate", null));

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
            var messages = body?.Errors?.Select(error => error.Message) ?? [];
            throw new InvalidOperationException(string.Join(" ", messages));
        }

        response.EnsureSuccessStatusCode();
    }

    private sealed record ErrorBody(List<ErrorItem>? Errors);

    private sealed record ErrorItem(string Field, string Message);
}
