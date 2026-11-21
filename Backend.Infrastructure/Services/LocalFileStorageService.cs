using SkiaSharp;
using UCR.EB.BioMicroscopeAdmin.Backend.Application.Services;

namespace UCR.EB.BioMicroscopeAdmin.Backend.Infrastructure.Services;

/// <summary>Stores images in a private folder that is never exposed as static content.</summary>
internal class LocalFileStorageService(string storagePath) : IFileStorageService
{
    private const int ThumbnailMaxSide = 480;
    private const int ThumbnailQuality = 82;
    private const long MaxPixels = 60_000_000;

    private readonly string _root = Path.GetFullPath(storagePath);
    private string ThumbnailsPath => Path.Combine(_root, "thumbnails");

    public async Task<string> SaveAsync(Stream fileStream, string fileName)
    {
        Directory.CreateDirectory(_root);
        var safeName = string.Concat(fileName.Select(c =>
            char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_'));
        var uniqueName = $"{Guid.NewGuid()}_{safeName}";
        var fullPath = Path.Combine(_root, uniqueName);
        await using var fs = File.Create(fullPath);
        await fileStream.CopyToAsync(fs);
        return $"/uploads/{uniqueName}";
    }

    public Task DeleteAsync(string fileUrl)
    {
        var fileName = Path.GetFileName(fileUrl);
        if (fileName.Length > 0)
        {
            foreach (var path in new[] { Path.Combine(_root, fileName), ThumbnailPathFor(fileName) })
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        return Task.CompletedTask;
    }

    public string? ValidateImage(Stream fileStream)
    {
        Span<byte> header = stackalloc byte[ImageFormatDetector.HeaderLength];
        var read = fileStream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        if (ImageFormatDetector.Detect(header[..read]) is null)
        {
            return "Only JPEG, PNG and WebP images are allowed.";
        }

        fileStream.Position = 0;
        using var data = SKData.Create(fileStream);
        using var codec = data is null ? null : SKCodec.Create(data);
        if (codec is null)
        {
            return "The image is damaged or cannot be read.";
        }

        return (long)codec.Info.Width * codec.Info.Height > MaxPixels
            ? $"The image cannot exceed {MaxPixels / 1_000_000} megapixels."
            : null;
    }

    public async Task<StoredFile?> OpenImageAsync(string fileUrl)
    {
        var fileName = Path.GetFileName(fileUrl);
        var fullPath = Path.Combine(_root, fileName);
        if (fileName.Length == 0 || !File.Exists(fullPath))
        {
            return null;
        }

        var stream = File.OpenRead(fullPath);
        var header = new byte[ImageFormatDetector.HeaderLength];
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        var contentType = ImageFormatDetector.Detect(header.AsSpan(0, read));
        if (contentType is null)
        {
            await stream.DisposeAsync();
            return null;
        }

        stream.Position = 0;
        return new StoredFile(stream, contentType);
    }

    public async Task<StoredFile?> OpenThumbnailAsync(string fileUrl)
    {
        var fileName = Path.GetFileName(fileUrl);
        var source = Path.Combine(_root, fileName);
        if (fileName.Length == 0 || !File.Exists(source))
        {
            return null;
        }

        var thumbnail = ThumbnailPathFor(fileName);
        if (!File.Exists(thumbnail) && !await Task.Run(() => TryCreateThumbnail(source, thumbnail)))
        {
            return null;
        }

        return new StoredFile(File.OpenRead(thumbnail), "image/jpeg");
    }

    public int ImportLegacyFiles(string legacyDirectory)
    {
        if (!Directory.Exists(legacyDirectory))
        {
            return 0;
        }

        Directory.CreateDirectory(_root);
        var moved = 0;
        foreach (var file in Directory.EnumerateFiles(legacyDirectory))
        {
            var target = Path.Combine(_root, Path.GetFileName(file));
            if (!File.Exists(target))
            {
                File.Move(file, target);
                moved++;
            }
        }

        return moved;
    }

    private string ThumbnailPathFor(string fileName) => Path.Combine(ThumbnailsPath, $"{fileName}.jpg");

    private bool TryCreateThumbnail(string source, string target)
    {
        using var codec = SKCodec.Create(source);
        if (codec is null || (long)codec.Info.Width * codec.Info.Height > MaxPixels)
        {
            return false;
        }

        using var bitmap = SKBitmap.Decode(codec);
        if (bitmap is null)
        {
            return false;
        }

        var scale = Math.Min(1f, (float)ThumbnailMaxSide / Math.Max(bitmap.Width, bitmap.Height));
        var width = Math.Max(1, (int)(bitmap.Width * scale));
        var height = Math.Max(1, (int)(bitmap.Height * scale));

        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.White);
        using var image = SKImage.FromBitmap(bitmap);
        surface.Canvas.DrawImage(image, new SKRect(0, 0, width, height),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

        using var snapshot = surface.Snapshot();
        using var encoded = snapshot.Encode(SKEncodedImageFormat.Jpeg, ThumbnailQuality);
        Directory.CreateDirectory(ThumbnailsPath);

        // Se escribe aparte para que una petición simultánea nunca lea un archivo a medias.
        var temporary = $"{target}.{Guid.NewGuid():N}.tmp";
        using (var output = File.Create(temporary))
        {
            encoded.SaveTo(output);
        }

        File.Move(temporary, target, overwrite: true);
        return true;
    }
}
