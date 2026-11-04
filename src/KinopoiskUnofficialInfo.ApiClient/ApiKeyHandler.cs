using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace KinopoiskUnofficialInfo.ApiClient
{
    /// <summary>
    /// Adds the current Kinopoisk API key to each outgoing request.
    /// Reading the key per request allows configuration changes without rebuilding the client.
    /// </summary>
    public sealed class ApiKeyHandler : DelegatingHandler
    {
        private readonly Func<string> _apiTokenProvider;

        public ApiKeyHandler(Func<string> apiTokenProvider)
        {
            _apiTokenProvider = apiTokenProvider ?? throw new ArgumentNullException(nameof(apiTokenProvider));
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var apiToken = _apiTokenProvider();
            if (string.IsNullOrWhiteSpace(apiToken))
                throw new InvalidOperationException("Kinopoisk API key is not configured. Set a personal API key in the plugin settings.");

            request.Headers.Remove("X-API-KEY");
            request.Headers.TryAddWithoutValidation("X-API-KEY", apiToken);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
