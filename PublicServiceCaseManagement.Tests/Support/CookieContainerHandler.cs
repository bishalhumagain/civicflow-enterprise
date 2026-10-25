using System.Net;

namespace PublicServiceCaseManagement.Tests.Support;

/// <summary>
/// Delegating handler that persists cookies between requests, emulating a
/// browser so the Identity auth cookie survives across HTTP calls.
/// </summary>
public sealed class CookieContainerHandler(HttpMessageHandler innerHandler)
    : DelegatingHandler(innerHandler)
{
    private readonly CookieContainer cookieContainer = new();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var cookieHeader = cookieContainer.GetCookieHeader(
            request.RequestUri!);

        if (!string.IsNullOrEmpty(cookieHeader))
        {
            request.Headers.Add("Cookie", cookieHeader);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.Headers.TryGetValues(
                "Set-Cookie",
                out var setCookieHeaders))
        {
            foreach (var setCookie in setCookieHeaders)
            {
                cookieContainer.SetCookies(
                    request.RequestUri!,
                    setCookie);
            }
        }

        return response;
    }
}
