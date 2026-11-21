using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UCR.EB.BioMicroscopeAdmin.Backend.Application.Services;
using UCR.EB.BioMicroscopeAdmin.Backend.Domain.Repositories;
using UCR.EB.BioMicroscopeAdmin.Backend.Infrastructure.Identity;
using UCR.EB.BioMicroscopeAdmin.Backend.Infrastructure.Persistence;
using UCR.EB.BioMicroscopeAdmin.Backend.Infrastructure.Repositories;
using UCR.EB.BioMicroscopeAdmin.Backend.Infrastructure.Services;

namespace UCR.EB.BioMicroscopeAdmin.Backend.Infrastructure;

/// <summary>Provides extension methods for configuring dependency injection in the infrastructure layer.</summary>
public static class DependencyInjection
{
    /// <summary>Registers infrastructure layer services into the dependency injection container.</summary>
    public static IServiceCollection AddInfrastructureLayerServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddIdentityServices();

        services.AddScoped<IGroupRepository, EfGroupRepository>();
        services.AddScoped<IBoxRepository, EfBoxRepository>();
        services.AddScoped<ISlideRepository, EfSlideRepository>();
        services.AddScoped<IImageRepository, EfImageRepository>();

        services.AddSingleton<IFileStorageService>(_ =>
        {
            var storagePath = configuration["Storage:Path"];
            return new LocalFileStorageService(string.IsNullOrWhiteSpace(storagePath)
                ? Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "images")
                : storagePath);
        });
        services.AddScoped<IImageFileLookup, EfImageFileLookup>();
        return services;
    }
}
