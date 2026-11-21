using System.Net;
using System.Net.Http.Json;
using UCR.EB.BioMicroscopeAdmin.Backend.Presentation.Endpoints;

namespace UCR.EB.BioMicroscopeAdmin.Backend.Api.Tests.Integration;

public sealed class UserManagementTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Users_RequireAdministrator()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateAnonymousClient().GetAsync("/api/users")).StatusCode);

        var reader = await factory.CreateClientForAsync(ApiFactory.ReaderEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/api/users")).StatusCode);
        var create = await reader.PostAsJsonAsync("/api/users", new { email = "x@test.local", password = ApiFactory.TestPassword });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task Administrator_CreatesReader_WhoCanLoginWithReaderRole()
    {
        var admin = await factory.CreateClientForAsync(ApiFactory.AdminEmail);
        var email = NewEmail();

        var created = await admin.PostAsJsonAsync("/api/users", new { email, password = ApiFactory.TestPassword });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var reader = await factory.CreateClientForAsync(email);
        var me = await reader.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");
        Assert.Equal(["Lector"], me!.Roles);
    }

    [Fact]
    public async Task Deactivate_ClosesOpenSessionAndBlocksLogin_UntilReactivated()
    {
        var admin = await factory.CreateClientForAsync(ApiFactory.AdminEmail);
        var email = NewEmail();
        var created = await (await admin.PostAsJsonAsync("/api/users", new { email, password = ApiFactory.TestPassword }))
            .Content.ReadFromJsonAsync<UserResponse>();
        var session = await factory.CreateClientForAsync(email);

        (await admin.PostAsync($"/api/users/{created!.Id}/deactivate", null)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/groups")).StatusCode);
        var blocked = await factory.CreateAnonymousClient().PostAsJsonAsync(
            "/api/auth/login", new { email, password = ApiFactory.TestPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, blocked.StatusCode);
        var users = await admin.GetFromJsonAsync<List<UserResponse>>("/api/users");
        Assert.False(users!.Single(u => u.Id == created.Id).IsActive);

        (await admin.PostAsync($"/api/users/{created.Id}/reactivate", null)).EnsureSuccessStatusCode();

        var again = await factory.CreateClientForAsync(email);
        Assert.Equal(HttpStatusCode.OK, (await again.GetAsync("/api/groups")).StatusCode);
    }

    [Fact]
    public async Task Administrator_CannotBeDeactivated()
    {
        var admin = await factory.CreateClientForAsync(ApiFactory.AdminEmail);
        var users = await admin.GetFromJsonAsync<List<UserResponse>>("/api/users");
        var self = users!.Single(u => u.Email == ApiFactory.AdminEmail);

        var response = await admin.PostAsync($"/api/users/{self.Id}/deactivate", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("no-es-correo", "ContrasenaValida1")]
    [InlineData("valido@test.local", "corta")]
    public async Task Create_WithInvalidData_Returns400(string email, string password)
    {
        var admin = await factory.CreateClientForAsync(ApiFactory.AdminEmail);

        var response = await admin.PostAsJsonAsync("/api/users", new { email, password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string NewEmail() => $"lector-{Guid.NewGuid():N}@test.local";
}
