using Microsoft.EntityFrameworkCore;
using UCR.EB.BioMicroscopeAdmin.Backend.Application.Services;
using UCR.EB.BioMicroscopeAdmin.Backend.Domain.Entities;
using UCR.EB.BioMicroscopeAdmin.Backend.Infrastructure.Persistence;

namespace UCR.EB.BioMicroscopeAdmin.Backend.Infrastructure.Services;

internal sealed class EfImageFileLookup(AppDbContext context) : IImageFileLookup
{
    public async Task<IReadOnlyList<string>> FindFileUrlsAsync(ImageOwner owner, int id)
    {
        var images = context.Images.AsNoTracking();
        IQueryable<Image> owned = owner switch
        {
            ImageOwner.Image => images.Where(i => i.Id == id),
            ImageOwner.Slide => images.Where(i => i.SlideId == id),
            ImageOwner.Box =>
                from image in images
                join slide in context.Slides on image.SlideId equals slide.Id
                where slide.BoxId == id
                select image,
            ImageOwner.Group =>
                from image in images
                join slide in context.Slides on image.SlideId equals slide.Id
                join box in context.Boxes on slide.BoxId equals box.Id
                where box.GroupId == id
                select image,
            _ => throw new ArgumentOutOfRangeException(nameof(owner))
        };

        var files = await owned.Select(i => i.FileUrl).ToListAsync();
        return files.Select(file => file.Value).ToList();
    }
}
