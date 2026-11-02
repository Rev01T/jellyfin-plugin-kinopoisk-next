using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KinopoiskUnofficialInfo.ApiClient
{
    public class KinopoiskApiClient : IKinopoiskApiClient
    {
        private readonly string _apiToken;
        private readonly ILogger<KinopoiskApiClient> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly Client _apiClient;
        private static readonly TimeSpan QuotaCooldown = TimeSpan.FromHours(1);
        private readonly object _quotaLock = new object();
        private DateTimeOffset _quotaBlockedUntil = DateTimeOffset.MinValue;
        private string _quotaExceededResponse = string.Empty;

        public KinopoiskApiClient(string apiToken, ILogger<KinopoiskApiClient> logger, IHttpClientFactory httpClientFactory)
        {
            _apiToken = apiToken ?? string.Empty;
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
            if (string.IsNullOrWhiteSpace(_apiToken))
                _logger.LogWarning("No Kinopoisk API token is configured. Set a personal token in the plugin settings.");
            _httpClientFactory = httpClientFactory ?? throw new System.ArgumentNullException(nameof(httpClientFactory));

            var httpClient = _httpClientFactory.CreateClient();
            if (!string.IsNullOrWhiteSpace(_apiToken))
                httpClient.DefaultRequestHeaders.Add("X-API-KEY", _apiToken);
            _apiClient = new Client(httpClient);
        }

        private async Task<T> Invoke<T>(Func<CancellationToken, Task<T>> method, CancellationToken? ct, [CallerMemberName] string memberName = "")
        {
            ApiException quotaException = null;
            lock (_quotaLock)
            {
                if (DateTimeOffset.UtcNow < _quotaBlockedUntil)
                {
                    quotaException = new ApiException("Kinopoisk API daily request limit exceeded.", 402, _quotaExceededResponse, new Dictionary<string, IEnumerable<string>>(), null);
                }
            }
            if (quotaException != null)
                throw quotaException;
            try
            {
                _logger.LogDebug($"{memberName} request starting...");
                var res = await method.Invoke(ct ?? CancellationToken.None).ConfigureAwait(false);
                _logger.LogDebug($"{memberName} request complete successfully");
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
                else
                {
                    _logger.LogError("Received non-success result status code {StatusCode} from Kinopoisk API, response content is: {Response}", e.StatusCode, e.Response);
                }
                throw;
            }
        }

        public Task<Film> GetSingleFilm(int filmId, CancellationToken? cancellationToken = null)
            => Invoke((ct) => _apiClient.FilmsAsync(filmId, ct), cancellationToken);

        public Task<ICollection<StaffResponse>> GetStaff(int filmId, CancellationToken? cancellationToken = null)
            => Invoke((ct) => _apiClient.StaffAllAsync(filmId, ct), cancellationToken);

        public Task<FilmSearchResponse> SearchByKeyword(string keyword, int page = 1, CancellationToken? cancellationToken = null)
            => Invoke((ct) => _apiClient.SearchByKeywordAsync(keyword, null, ct), cancellationToken);

        public Task<PersonResponse> GetPerson(int personId, CancellationToken? cancellationToken = null)
            => Invoke((ct) => _apiClient.StaffAsync(personId, ct), cancellationToken);

        public Task<VideoResponse> GetTrailers(int filmId, CancellationToken? cancellationToken = null)
        {
            return Invoke(async (ct) => {
                try {
                    return await _apiClient.VideosAsync(filmId, ct);
                } catch (ApiException e)
                {
                    if (e.StatusCode == 404)
                        return new VideoResponse();
                    throw;
                }
            }, cancellationToken);
        }
    }
}
