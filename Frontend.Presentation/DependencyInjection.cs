using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using UCR.EB.BioMicroscopeAdmin.Frontend.Presentation.Security;

namespace UCR.EB.BioMicroscopeAdmin.Frontend.Presentation;

/// <summary>Provides extension methods for configuring dependency injection in the presentation layer.</summary>
public static class DependencyInjection
{
    /// <summary>Registers presentation layer services into the dependency injection container.</summary>
    public static IServiceCollection AddPresentationLayerServices(this IServiceCollection services)
    {
        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();
        services.AddScoped<CookieAuthStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CookieAuthStateProvider>());
        return services;
    }
}
