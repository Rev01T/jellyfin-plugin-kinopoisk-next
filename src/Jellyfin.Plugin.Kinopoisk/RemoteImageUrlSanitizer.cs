using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Kinopoisk
{
    public class RemoteImageUrlSanitizer
    {
        private const int MaxRedirects = 5;
        private readonly HttpClient _httpClient;

        public RemoteImageUrlSanitizer(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<string> SanitizeRemoteImageUrl(string url, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url) || url.Contains("no-poster", StringComparison.OrdinalIgnoreCase))
                return null;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var currentUri)
                || (currentUri.Scheme != Uri.UriSchemeHttp && currentUri.Scheme != Uri.UriSchemeHttps))
                return null;

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var redirect = 0; redirect <= MaxRedirects; redirect++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!visited.Add(currentUri.AbsoluteUri))
                    return null;

                using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
                using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false);

                if ((int)response.StatusCode is >= 200 and <= 299)
                    return currentUri.AbsoluteUri;

                if (!response.Headers.Location.IsAbsoluteUriOrResolvable(currentUri, out var nextUri))
                    return null;

                if (nextUri.Scheme != Uri.UriSchemeHttp && nextUri.Scheme != Uri.UriSchemeHttps)
                    return null;

                if (nextUri.AbsoluteUri.Contains("no-poster", StringComparison.OrdinalIgnoreCase))
                    return null;

                currentUri = nextUri;
            }

            return null;
        }
    }

    internal static class UriExtensions
    {
        public static bool IsAbsoluteUriOrResolvable(this Uri location, Uri baseUri, out Uri resolved)
        {
            resolved = null;
            if (location == null)
                return false;

            resolved = location.IsAbsoluteUri ? location : new Uri(baseUri, location);
            return true;
        }
    }
}
