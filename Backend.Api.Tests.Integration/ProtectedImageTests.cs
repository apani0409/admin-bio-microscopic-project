using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace UCR.EB.BioMicroscopeAdmin.Backend.Api.Tests.Integration;

public sealed class ProtectedImageTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    [Fact]
    public async Task File_RequiresSession_AndIsServedToReader()
    {
        var admin = await factory.CreateClientForAsync(ApiFactory.AdminEmail);
        var image = await UploadAsync(admin, await CreateSlideAsync(admin), 1);

        var anonymous = factory.CreateAnonymousClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/images/{image.Id}/file")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/images/{image.Id}/thumbnail")).StatusCode);

        var reader = await factory.CreateClientForAsync(ApiFactory.ReaderEmail);
        var file = await reader.GetAsync($"/api/images/{image.Id}/file");
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("image/png", file.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await file.Content.ReadAsByteArrayAsync());

        var thumbnail = await reader.GetAsync($"/api/images/{image.Id}/thumbnail");
        Assert.Equal(HttpStatusCode.OK, thumbnail.StatusCode);
        Assert.Equal("image/jpeg", thumbnail.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task StoredFile_IsNotReachableThroughFormerPublicRoute()
    {
        var admin = await factory.CreateClientForAsync(ApiFactory.AdminEmail);
        var image = await UploadAsync(admin, await CreateSlideAsync(admin), 1);

        var response = await factory.CreateAnonymousClient().GetAsync(image.FileUrl);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("pagina.html", "<script>alert(1)</script>")]
    [InlineData("vector.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\" onload=\"alert(1)\"/>")]
    [InlineData("falsa.png", "esto no es una imagen")]
    public async Task Upload_RejectsNonImages(string fileName, string content)
    {
        var admin = await factory.CreateClientForAsync(ApiFactory.AdminEmail);
        var slideId = await CreateSlideAsync(admin);

        var response = await PostFileAsync(admin, slideId, Encoding.UTF8.GetBytes(content), fileName, 1);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_EnforcesUniquePositionAndMaximum()
    {
        var admin = await factory.CreateClientForAsync(ApiFactory.AdminEmail);
        var slideId = await CreateSlideAsync(admin);
        for (var order = 1; order <= 5; order++)
        {
            await UploadAsync(admin, slideId, order);
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await PostFileAsync(admin, slideId, Png, "repetida.png", 1)).StatusCode);
        await UploadAsync(admin, slideId, 6);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostFileAsync(admin, slideId, Png, "septima.png", 6)).StatusCode);
    }

    [Fact]
    public async Task UpdateOrder_SwapsOccupiedPosition()
    {
        var admin = await factory.CreateClientForAsync(ApiFactory.AdminEmail);
        var slideId = await CreateSlideAsync(admin);
        var first = await UploadAsync(admin, slideId, 1);
        var second = await UploadAsync(admin, slideId, 2);

        (await admin.PutAsJsonAsync($"/api/images/{second.Id}", new { order = 1 })).EnsureSuccessStatusCode();

        var images = await admin.GetFromJsonAsync<List<ImageResult>>($"/api/slides/{slideId}/images");
        Assert.Equal(1, images!.Single(i => i.Id == second.Id).Order);
        Assert.Equal(2, images!.Single(i => i.Id == first.Id).Order);
    }

    [Fact]
    public async Task Delete_RemovesStoredFile()
    {
        var admin = await factory.CreateClientForAsync(ApiFactory.AdminEmail);
        var image = await UploadAsync(admin, await CreateSlideAsync(admin), 1);
        await admin.GetAsync($"/api/images/{image.Id}/thumbnail");
        var path = StoredPath(image);
        Assert.True(File.Exists(path));

        (await admin.DeleteAsync($"/api/images/{image.Id}")).EnsureSuccessStatusCode();

        Assert.False(File.Exists(path));
        Assert.False(File.Exists(Path.Combine(factory.StoragePath, "thumbnails", $"{Path.GetFileName(path)}.jpg")));
    }

    [Fact]
    public async Task DeleteGroup_RemovesFilesOfItsCascade()
    {
        var admin = await factory.CreateClientForAsync(ApiFactory.AdminEmail);
        var (groupId, slideId) = await CreateHierarchyAsync(admin);
        var image = await UploadAsync(admin, slideId, 1);
        var path = StoredPath(image);

        (await admin.DeleteAsync($"/api/groups/{groupId}")).EnsureSuccessStatusCode();

        Assert.False(File.Exists(path));
    }

    private string StoredPath(ImageResult image) =>
        Path.Combine(factory.StoragePath, Path.GetFileName(image.FileUrl));

    private static async Task<int> CreateSlideAsync(HttpClient client) =>
        (await CreateHierarchyAsync(client)).SlideId;

    private static async Task<(int GroupId, int SlideId)> CreateHierarchyAsync(HttpClient client)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var group = await PostAsync(client, "/api/groups", new { name = $"Grupo {suffix}" });
        var number = Random.Shared.Next(1, 100_000);
        var box = await PostAsync(client, "/api/boxes", new { number, name = $"Caja {suffix}", groupId = group });
        var slide = await PostAsync(client, "/api/slides", new { code = suffix, name = $"Lamina {suffix}", boxId = box });
        return (group, slide);
    }

    private static async Task<int> PostAsync(HttpClient client, string route, object body)
    {
        var response = await client.PostAsJsonAsync(route, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>())!.Id;
    }

    private static async Task<ImageResult> UploadAsync(HttpClient client, int slideId, int order)
    {
        var response = await PostFileAsync(client, slideId, Png, $"vista{order}.png", order);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ImageResult>())!;
    }

    private static Task<HttpResponseMessage> PostFileAsync(
        HttpClient client, int slideId, byte[] bytes, string fileName, int order)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var form = new MultipartFormDataContent { { file, "file", fileName }, { new StringContent(order.ToString()), "order" } };
        return client.PostAsync($"/api/slides/{slideId}/images", form);
    }

    private sealed record IdResult(int Id);

    private sealed record ImageResult(int Id, string FileUrl, int Order);
}
