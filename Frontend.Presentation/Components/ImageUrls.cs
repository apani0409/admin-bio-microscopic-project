namespace UCR.EB.BioMicroscopeAdmin.Frontend.Presentation.Components;

/// <summary>Protected image routes. The session cookie authorizes them from an img tag.</summary>
public static class ImageUrls
{
    public static string File(int imageId) => $"api/images/{imageId}/file";

    public static string Thumbnail(int imageId) => $"api/images/{imageId}/thumbnail";
}
