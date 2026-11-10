using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace KinopoiskUnofficialInfo.ApiClient
{
    public class KinopoiskApiClient : IKinopoiskApiClient
    {
        private readonly ILogger<KinopoiskApiClient> _logger;
        private readonly Client _apiClient;
        private readonly HttpClient _httpClient;
        private static readonly TimeSpan QuotaCooldown = TimeSpan.FromHours(1);
        private readonly object _quotaLock = new object();
        private DateTimeOffset _quotaBlockedUntil = DateTimeOffset.MinValue;
        private string _quotaExceededResponse = string.Empty;
        private static readonly TimeSpan MinimumRequestInterval = TimeSpan.FromMilliseconds(110);
        private readonly SemaphoreSlim _requestStartLock = new SemaphoreSlim(1, 1);
        private DateTimeOffset _nextRequestAt = DateTimeOffset.MinValue;

        public KinopoiskApiClient(string apiToken, ILogger<KinopoiskApiClient> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            if (string.IsNullOrWhiteSpace(apiToken))
                _logger.LogWarning("No Kinopoisk API token is configured. Set a personal token in the plugin settings.");

            if (httpClientFactory == null)
                throw new ArgumentNullException(nameof(httpClientFactory));

            _httpClient = httpClientFactory.CreateClient("KinopoiskApiClient");
            _apiClient = new Client(_httpClient);
        }

        private async Task<T> Invoke<T>(Func<CancellationToken, Task<T>> method, CancellationToken? ct, [CallerMemberName] string memberName = "")
        {
            ApiException quotaException = null;
            lock (_quotaLock)
            {
                if (DateTimeOffset.UtcNow < _quotaBlockedUntil)
                    quotaException = new ApiException("Kinopoisk API daily request limit exceeded.", 402, _quotaExceededResponse, new Dictionary<string, IEnumerable<string>>(), null);
            }
            if (quotaException != null)
                throw quotaException;

            try
            {
                var requestCancellation = ct ?? CancellationToken.None;
                await WaitForRequestSlot(requestCancellation).ConfigureAwait(false);
                _logger.LogDebug("{ApiMethod} request starting...", memberName);
                var res = await method.Invoke(requestCancellation).ConfigureAwait(false);
                _logger.LogDebug("{ApiMethod} request completed successfully", memberName);
                return res;
            }
            catch (ApiException e)
            {
                if (e.StatusCode == 402)
                {
                    var shouldLog = false;
                    DateTimeOffset blockedUntil = DateTimeOffset.MinValue;
                    var now = DateTimeOffset.UtcNow;
                    lock (_quotaLock)
                    {
                        if (now >= _quotaBlockedUntil)
                        {
                            _quotaBlockedUntil = now.Add(QuotaCooldown);
                            _quotaExceededResponse = e.Response ?? string.Empty;
                            blockedUntil = _quotaBlockedUntil;
                            shouldLog = true;
                        }
                    }
                    if (shouldLog)
                        _logger.LogWarning("Kinopoisk API quota exhausted. Requests are paused until {BlockedUntil:u}", blockedUntil);
                }
                else if (e.StatusCode == 401)
                    _logger.LogWarning("Kinopoisk API rejected the configured API key (HTTP 401). Check the key in plugin settings.");
                else
                    _logger.LogError("Received non-success result status code {StatusCode} from Kinopoisk API, response content is: {Response}", e.StatusCode, e.Response);
                throw;
            }
        }

        private async Task WaitForRequestSlot(CancellationToken cancellationToken)
        {
            await _requestStartLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var delay = _nextRequestAt - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                _nextRequestAt = DateTimeOffset.UtcNow.Add(MinimumRequestInterval);
            }
            finally
            {
                _requestStartLock.Release();
            }
        }

        private async Task<T> GetJson<T>(string relativeUrl, CancellationToken cancellationToken)
        {
            var requestUri = new Uri(_apiClient.BaseUrl.TrimEnd('/') + relativeUrl, UriKind.Absolute);
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            var responseBody = response.Content == null
                ? string.Empty
                : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var headers = response.Headers
                    .Concat(response.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
                    .GroupBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.OrdinalIgnoreCase);
                throw new ApiException(
                    $"Kinopoisk API request failed with HTTP {(int)response.StatusCode}.",
                    (int)response.StatusCode,
                    responseBody,
                    headers,
                    null);
            }

            return JsonConvert.DeserializeObject<T>(responseBody)
                ?? throw new ApiException("Kinopoisk API returned an empty response.", (int)response.StatusCode, responseBody, new Dictionary<string, IEnumerable<string>>(), null);
        }

        public Task<Film> GetSingleFilm(int filmId, CancellationToken? cancellationToken = null)
            => Invoke(ct => _apiClient.FilmsAsync(filmId, ct), cancellationToken);

        public Task<FilmSearchByFiltersResponse> GetFilmByImdbId(string imdbId, CancellationToken? cancellationToken = null)
        {
            if (string.IsNullOrWhiteSpace(imdbId))
                throw new ArgumentException("IMDb ID is required.", nameof(imdbId));

            var query = Uri.EscapeDataString(imdbId.Trim());
            return Invoke(ct => GetJson<FilmSearchByFiltersResponse>($"/api/v2.2/films?imdbId={query}", ct), cancellationToken);
        }

        public Task<ApiKeyInfoResponse> GetApiKeyInfo(string apiKey, CancellationToken? cancellationToken = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key is required.", nameof(apiKey));

            var escapedKey = Uri.EscapeDataString(apiKey.Trim());
            return Invoke(ct => GetJson<ApiKeyInfoResponse>($"/api/v1/api_keys/{escapedKey}", ct), cancellationToken);
        }

        public Task<ICollection<StaffResponse>> GetStaff(int filmId, CancellationToken? cancellationToken = null)
            => Invoke(ct => _apiClient.StaffAllAsync(filmId, ct), cancellationToken);

        public Task<FilmSearchResponse> SearchByKeyword(string keyword, int page = 1, CancellationToken? cancellationToken = null)
            => Invoke(ct => _apiClient.SearchByKeywordAsync(keyword, null, ct), cancellationToken);

        public Task<PersonResponse> GetPerson(int personId, CancellationToken? cancellationToken = null)
            => Invoke(ct => _apiClient.StaffAsync(personId, ct), cancellationToken);

        public Task<FilmImagesResponse> GetImages(int filmId, string type, int page = 1, CancellationToken? cancellationToken = null)
        {
            if (filmId < 1 || filmId > 15000000)
                throw new ArgumentOutOfRangeException(nameof(filmId));
            if (page < 1 || page > 20)
                throw new ArgumentOutOfRangeException(nameof(page));
            if (string.IsNullOrWhiteSpace(type))
                throw new ArgumentException("Image type is required.", nameof(type));

            var escapedType = Uri.EscapeDataString(type.Trim().ToUpperInvariant());
            var relativeUrl = $"/api/v2.2/films/{filmId}/images?type={escapedType}&page={page}";
            return Invoke(ct => GetJson<FilmImagesResponse>(relativeUrl, ct), cancellationToken);
        }

        public Task<VideoResponse> GetTrailers(int filmId, CancellationToken? cancellationToken = null)
        {
            return Invoke(async ct =>
            {
                try
                {
                    return await _apiClient.VideosAsync(filmId, ct).ConfigureAwait(false);
                }
                catch (ApiException e) when (e.StatusCode == 404)
                {
                    return new VideoResponse();
                }
            }, cancellationToken);
        }
    }
}
