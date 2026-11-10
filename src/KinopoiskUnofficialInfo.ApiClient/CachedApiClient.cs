using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace KinopoiskUnofficialInfo.ApiClient
{
    public class CachedKinopoiskApiClient : IKinopoiskApiClient
    {
        private readonly IKinopoiskApiClient _innerClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CachedKinopoiskApiClient> _logger;

        public CachedKinopoiskApiClient(IKinopoiskApiClient innerClient, IMemoryCache cache, ILogger<CachedKinopoiskApiClient> logger)
        {
            _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public CachedKinopoiskApiClient(string apiToken, ILogger<KinopoiskApiClient> innerLogger, System.Net.Http.IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<CachedKinopoiskApiClient> logger)
            : this(new KinopoiskApiClient(apiToken, innerLogger, httpClientFactory), cache, logger)
        {
        }

        public Task<PersonResponse> GetPerson(int personId, CancellationToken? cancellationToken = null)
            => TryGetValue(GenerateKey(nameof(GetPerson), personId), c => c.GetPerson(personId, cancellationToken));

        public Task<Film> GetSingleFilm(int filmId, CancellationToken? cancellationToken = null)
            => TryGetValue(GenerateKey(nameof(GetSingleFilm), filmId), c => c.GetSingleFilm(filmId, cancellationToken));

        public Task<FilmSearchByFiltersResponse> GetFilmByImdbId(string imdbId, CancellationToken? cancellationToken = null)
            => TryGetValue(GenerateKey(nameof(GetFilmByImdbId), imdbId), c => c.GetFilmByImdbId(imdbId, cancellationToken));

        public Task<ApiKeyInfoResponse> GetApiKeyInfo(string apiKey, CancellationToken? cancellationToken = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key is required.", nameof(apiKey));

            // Never put the API key itself into an IMemoryCache key or diagnostic log.
            var keyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey.Trim())));
            return TryGetValue(GenerateKey(nameof(GetApiKeyInfo), keyHash), c => c.GetApiKeyInfo(apiKey, cancellationToken), TimeSpan.FromMinutes(15));
        }

        public Task<SeasonResponse> GetSeasons(int filmId, CancellationToken? cancellationToken = null)
            => TryGetValue(GenerateKey(nameof(GetSeasons), filmId), c => c.GetSeasons(filmId, cancellationToken));

        public Task<DistributionResponse> GetDistributions(int filmId, CancellationToken? cancellationToken = null)
            => TryGetValue(GenerateKey(nameof(GetDistributions), filmId), c => c.GetDistributions(filmId, cancellationToken));

        public Task<ICollection<StaffResponse>> GetStaff(int filmId, CancellationToken? cancellationToken = null)
            => TryGetValue(GenerateKey(nameof(GetStaff), filmId), c => c.GetStaff(filmId, cancellationToken));

        public Task<VideoResponse> GetTrailers(int filmId, CancellationToken? cancellationToken = null)
            => TryGetValue(GenerateKey(nameof(GetTrailers), filmId), c => c.GetTrailers(filmId, cancellationToken));

        public Task<FilmImagesResponse> GetImages(int filmId, string type, int page = 1, CancellationToken? cancellationToken = null)
            => TryGetValue(GenerateKey(nameof(GetImages), filmId, type, page), c => c.GetImages(filmId, type, page, cancellationToken));

        public Task<FilmSearchResponse> SearchByKeyword(string keyword, int page = 1, CancellationToken? cancellationToken = null)
            => TryGetValue(GenerateKey(nameof(SearchByKeyword), keyword, page), c => c.SearchByKeyword(keyword, page, cancellationToken));

        private static string GenerateKey(params object[] objects)
        {
            var key = new StringBuilder();
            foreach (var value in objects)
                key.Append(value?.ToString() ?? "<null>").Append(';');
            return key.ToString();
        }

        private Task<T> TryGetValue<T>(string key, Func<IKinopoiskApiClient, Task<T>> resultFactory, TimeSpan? expiration = null)
        {
            return _cache.GetOrCreateAsync(key, async entry =>
            {
                _logger.LogDebug("Cache entry for {CacheEntryType} not found; requesting from server", typeof(T).Name);
                entry.AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(10);
                // Exceptions are deliberately allowed to propagate. In particular, quota and
                // authentication failures must not be cached as successful empty API responses.
                return await resultFactory(_innerClient).ConfigureAwait(false);
            });
        }
    }
}
