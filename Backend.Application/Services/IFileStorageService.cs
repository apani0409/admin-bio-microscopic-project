namespace UCR.EB.BioMicroscopeAdmin.Backend.Application.Services;

/// <summary>An image opened from storage, ready to be sent to an authorized user.</summary>
public sealed record StoredFile(Stream Content, string ContentType);

/// <summary>Contract for saving, reading and deleting image files on the underlying storage.</summary>
public interface IFileStorageService
{
    /// <summary>Saves a file stream to storage and returns the key persisted for it.</summary>
    Task<string> SaveAsync(Stream fileStream, string fileName);

    /// <summary>Deletes the file with the given key and its thumbnail.</summary>
    Task DeleteAsync(string fileUrl);

    /// <summary>Returns an error message when the stream is not a supported image, otherwise null.</summary>
    string? ValidateImage(Stream fileStream);

    /// <summary>Opens the original image, or returns null if it is missing or not a supported image.</summary>
    Task<StoredFile?> OpenImageAsync(string fileUrl);

    /// <summary>Opens the JPEG thumbnail, creating it when needed.</summary>
    Task<StoredFile?> OpenThumbnailAsync(string fileUrl);

    /// <summary>Moves files from a former public folder into private storage.</summary>
    int ImportLegacyFiles(string legacyDirectory);
}

/// <summary>Identifies which entity owns the images removed by a delete.</summary>
public enum ImageOwner
{
    Group,
    Box,
    Slide,
    Image
}

/// <summary>Finds the stored files that belong to an entity.</summary>
public interface IImageFileLookup
{
    Task<IReadOnlyList<string>> FindFileUrlsAsync(ImageOwner owner, int id);
}

/// <summary>Detects supported image formats from their file signature.</summary>
public static class ImageFormatDetector
{
    public const int HeaderLength = 12;

    public static string? Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (header.Length >= 8 && header[..8].SequenceEqual<byte>([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return "image/png";
        }

        if (header.Length >= HeaderLength
            && header[..4].SequenceEqual("RIFF"u8)
            && header[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        return null;
    }
}
