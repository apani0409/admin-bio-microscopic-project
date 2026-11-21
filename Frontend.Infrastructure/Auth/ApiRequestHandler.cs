using System.Net;
using UCR.EB.BioMicroscopeAdmin.Frontend.Application.Services;

namespace UCR.EB.BioMicroscopeAdmin.Frontend.Infrastructure.Auth;

/// <summary>Adds the anti-CSRF header and reports expired sessions.</summary>
internal sealed class ApiRequestHandler(AuthSessionNotifier notifier) : DelegatingHandler
{
    private const string CsrfHeaderName = "X-Requested-With";
    private const string CsrfHeaderValue = "BioMicroscope";

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.TryAddWithoutValidation(CsrfHeaderName, CsrfHeaderValue);
        var response = await base.SendAsync(request, cancellationToken);

        // Un 401 en /auth/* es una respuesta normal del login o de "sin sesión".
        var isAuthRoute = request.RequestUri?.AbsolutePath.Contains("/auth/", StringComparison.OrdinalIgnoreCase) == true;
        if (response.StatusCode == HttpStatusCode.Unauthorized && !isAuthRoute)
        {
            notifier.NotifySessionExpired();
        }

        return response;
    }
}
