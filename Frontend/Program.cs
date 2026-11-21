using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using UCR.EB.BioMicroscopeAdmin.Frontend;
using UCR.EB.BioMicroscopeAdmin.Frontend.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();
// El API hospeda este front: comparten origen y la cookie de sesión viaja sola.
builder.Services.AddFrontendServices(new Uri(new Uri(builder.HostEnvironment.BaseAddress), "api/").ToString());

await builder.Build().RunAsync();
