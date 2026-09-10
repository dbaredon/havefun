using Gnist.Models;
using Microsoft.Extensions.Options;

namespace Gnist.Services;

public sealed class FrontendLinks(IOptions<PartyOptions> options)
{
    public string? JoinUrl(string code, bool pages, HttpRequest request)
    {
        code = Uri.EscapeDataString(code.Trim().ToUpperInvariant());
        if (pages)
        {
            var frontend = options.Value.FrontendBaseUrl.TrimEnd('/');
            if (frontend.Length == 0) return null;
            return $"{frontend}/join/?code={code}";
        }
        var backend = options.Value.PublicBaseUrl.TrimEnd('/');
        if (backend.Length == 0) backend = $"{request.Scheme}://{request.Host}{request.PathBase}";
        return $"{backend}/join/{code}";
    }

    public bool AllowsOrigin(string origin, HttpRequest request)
    {
        if (origin == $"{request.Scheme}://{request.Host}") return true;
        return new[] { options.Value.FrontendBaseUrl, options.Value.PublicBaseUrl }.Any(url =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            string.Equals(origin, uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase));
    }
}
