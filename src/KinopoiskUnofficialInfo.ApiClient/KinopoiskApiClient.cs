using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KinopoiskUnofficialInfo.ApiClient
{
    public class KinopoiskApiClient : IKinopoiskApiClient
    {
        public const string DefaultBaseUrl = "https://api.poiskkino.dev";

        private const int MaxSeasonPages = 10;
        // The API allows 5 requests per second (the x-ratelimit-limit header): 4 per second leaves a margin.
        private static readonly TimeSpan DefaultRequestInterval = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(5);
        private static readonly Regex IdSegment = new Regex(@"/\d+", RegexOptions.Compiled);

        private readonly ILogger<KinopoiskApiClient> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly ApiJson _json;
        private readonly TimeSpan _requestInterval;
        private readonly Func<DateTimeOffset> _utcNow;
        private readonly object _quotaLock = new object();
        private DateTimeOffset _quotaBlockedUntil = DateTimeOffset.MinValue;
        private readonly SemaphoreSlim _requestStartLock = new SemaphoreSlim(1, 1);
        private DateTimeOffset _nextRequestAt = DateTimeOffset.MinValue;

        public KinopoiskApiClient(
            string apiToken,
            ILogger<KinopoiskApiClient> logger,
            IHttpClientFactory httpClientFactory,
            Func<DateTimeOffset> utcNow = null,
            string baseUrl = DefaultBaseUrl,
            TimeSpan? requestInterval = null)
        {
            _requestInterval = requestInterval ?? DefaultRequestInterval;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
            if (string.IsNullOrWhiteSpace(apiToken))
                _logger.LogWarning("No poiskkino.dev API token is configured. Set a personal token in the plugin settings.");

            if (httpClientFactory == null)
                throw new ArgumentNullException(nameof(httpClientFactory));

            _httpClient = httpClientFactory.CreateClient("KinopoiskApiClient");
            _baseUrl = (string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl).TrimEnd('/');
            _json = new ApiJson(_logger);
        }

        public Task<MovieDto> GetMovie(int movieId, CancellationToken? cancellationToken = null)
            => Invoke(async ct =>
            {
                try
                {
                    return await GetJson<MovieDto>($"/v1.5/movie/{movieId}", ct).ConfigureAwait(false);
                }
                catch (ApiException e) when (e.StatusCode == 400 || e.StatusCode == 404)
                {
                    // 400: the ID is outside of the range the API accepts (250..99999999).
                    _logger.LogDebug("Kinopoisk title {MovieId} was not found (HTTP {StatusCode})", movieId, e.StatusCode);
                    return null;
                }
            }, cancellationToken);

        public Task<IReadOnlyList<MovieDto>> FindMoviesByImdbId(string imdbId, CancellationToken? cancellationToken = null)
        {
            if (string.IsNullOrWhiteSpace(imdbId))
                throw new ArgumentException("IMDb ID is required.", nameof(imdbId));

            var escaped = Uri.EscapeDataString(imdbId.Trim());
            return Invoke<IReadOnlyList<MovieDto>>(async ct =>
            {
                var response = await GetJson<CursorResponse<MovieDto>>($"/v1.5/movie?externalId.imdb={escaped}&limit=2", ct).ConfigureAwait(false);
                return response.Docs?.Where(movie => movie != null).ToList() ?? new List<MovieDto>();
            }, cancellationToken);
        }

        public Task<SearchResponse<MovieDto>> SearchMovies(string query, int page = 1, CancellationToken? cancellationToken = null)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("A search query is required.", nameof(query));

            var escaped = Uri.EscapeDataString(query.Trim());
            var pageNumber = Math.Max(page, 1);
            return Invoke(ct => GetJson<SearchResponse<MovieDto>>($"/v1.5/movie/search?query={escaped}&page={pageNumber}&limit=10", ct), cancellationToken);
        }

        public Task<IReadOnlyList<SeasonDto>> GetSeasons(int movieId, CancellationToken? cancellationToken = null)
            => Invoke<IReadOnlyList<SeasonDto>>(async ct =>
            {
                var seasons = new List<SeasonDto>();
                string next = null;
                for (var page = 0; page < MaxSeasonPages; page++)
                {
                    var url = $"/v1.5/season?movieId={movieId}&limit=250";
                    if (!string.IsNullOrEmpty(next))
                        url += "&next=" + Uri.EscapeDataString(next);

                    CursorResponse<SeasonDto> response;
                    try
                    {
                        response = await GetJson<CursorResponse<SeasonDto>>(url, ct).ConfigureAwait(false);
                    }
                    catch (ApiException e) when (e.StatusCode == 400 || e.StatusCode == 404)
                    {
                        _logger.LogDebug("Seasons of the Kinopoisk title {MovieId} were not found (HTTP {StatusCode})", movieId, e.StatusCode);
                        break;
                    }

                    if (response.Docs != null)
                        seasons.AddRange(response.Docs.Where(season => season != null));

                    if (!response.HasNext || string.IsNullOrEmpty(response.Next))
                        break;
                    next = response.Next;
                }

                return seasons.OrderBy(season => season.Number ?? int.MaxValue).ToList();
            }, cancellationToken);

        public Task<PersonDto> GetPerson(int personId, CancellationToken? cancellationToken = null)
            => Invoke(async ct =>
            {
                try
                {
                    return await GetJson<PersonDto>($"/v1.5/person/{personId}", ct).ConfigureAwait(false);
                }
                catch (ApiException e) when (e.StatusCode == 400 || e.StatusCode == 404)
                {
                    _logger.LogDebug("Kinopoisk person {PersonId} was not found (HTTP {StatusCode})", personId, e.StatusCode);
                    return null;
                }
            }, cancellationToken);

        // The token info is not counted against the limit, so it is available even when the quota
        // is exhausted and the other requests are paused.
        public Task<TokenInfoDto> GetTokenInfo(CancellationToken? cancellationToken = null)
            => FetchTokenInfo(cancellationToken ?? CancellationToken.None);

        private async Task<T> Invoke<T>(Func<CancellationToken, Task<T>> method, CancellationToken? cancellationToken, [CallerMemberName] string memberName = "")
        {
            ThrowIfPaused();

            try
            {
                _logger.LogDebug("{ApiMethod} request starting...", memberName);
                var result = await method(cancellationToken ?? CancellationToken.None).ConfigureAwait(false);
                _logger.LogDebug("{ApiMethod} request completed successfully", memberName);
                return result;
            }
            catch (ApiException e) when (e.IsQuotaExceeded)
            {
                // Already reported when the pause started.
                throw;
            }
            catch (ApiException e)
            {
                if (e.StatusCode == 401)
                    _logger.LogWarning("poiskkino.dev rejected the configured API token (HTTP 401). Check the token in the plugin settings.");
                else
                    _logger.LogError("Received non-success result status code {StatusCode} from poiskkino.dev, response content is: {Response}", e.StatusCode, e.Response);
                throw;
            }
        }

        private void ThrowIfPaused()
        {
            DateTimeOffset until;
            lock (_quotaLock)
            {
                if (_utcNow() >= _quotaBlockedUntil)
                    return;
                until = _quotaBlockedUntil;
            }

            throw new ApiException(
                $"poiskkino.dev daily request limit exceeded. Requests are paused until {until:u}.",
                402,
                string.Empty,
                isQuotaExceeded: true);
        }

        private async Task<T> GetJson<T>(string relativeUrl, CancellationToken cancellationToken) where T : class
        {
            for (var attempt = 1; ; attempt++)
            {
                await WaitForRequestSlot(cancellationToken).ConfigureAwait(false);
                var response = await Send(relativeUrl, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccess)
                {
                    return _json.Deserialize<T>(response.Body, EndpointName(relativeUrl))
                        ?? throw new ApiException("poiskkino.dev returned an empty response.", response.StatusCode, response.Body);
                }

                var quotaException = await DetectQuotaExhaustion(response, cancellationToken).ConfigureAwait(false);
                if (quotaException != null)
                    throw quotaException;

                // HTTP 429 while there are requests left in the quota is a short-term rate limit: one retry.
                if (response.StatusCode == 429 && attempt == 1)
                {
                    var delay = response.RetryAfter ?? DefaultRetryDelay;
                    if (delay < TimeSpan.Zero) delay = DefaultRetryDelay;
                    if (delay > MaxRetryDelay) delay = MaxRetryDelay;
                    _logger.LogDebug("poiskkino.dev answered HTTP 429, retrying in {Delay}", delay);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                throw new ApiException(
                    $"poiskkino.dev request failed with HTTP {response.StatusCode}: {response.ErrorMessage}",
                    response.StatusCode,
                    response.Body);
            }
        }

        private async Task<TokenInfoDto> FetchTokenInfo(CancellationToken cancellationToken)
        {
            var response = await Send("/v1.5/token", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccess)
                throw new ApiException(
                    $"poiskkino.dev token info request failed with HTTP {response.StatusCode}: {response.ErrorMessage}",
                    response.StatusCode,
                    response.Body);

            return _json.Deserialize<TokenInfoDto>(response.Body, "/v1.5/token")
                ?? throw new ApiException("poiskkino.dev returned an empty token info.", response.StatusCode, response.Body);
        }

        private async Task<RawResponse> Send(string relativeUrl, CancellationToken cancellationToken)
        {
            var requestUri = new Uri(_baseUrl + relativeUrl, UriKind.Absolute);
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            var body = response.Content == null
                ? string.Empty
                : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            TimeSpan? retryAfter = null;
            var retryHeader = response.Headers.RetryAfter;
            if (retryHeader != null)
                retryAfter = retryHeader.Delta ?? (retryHeader.Date.HasValue ? retryHeader.Date.Value - _utcNow() : (TimeSpan?)null);

            // Without Retry-After the API tells when the rate limit window ends in x-ratelimit-reset (seconds).
            if (retryAfter == null
                && response.Headers.TryGetValues("x-ratelimit-reset", out var resetValues)
                && int.TryParse(resetValues.FirstOrDefault(), NumberStyles.None, CultureInfo.InvariantCulture, out var resetSeconds)
                && resetSeconds > 0 && resetSeconds <= 60)
                retryAfter = TimeSpan.FromSeconds(resetSeconds);

            return new RawResponse((int)response.StatusCode, body, retryAfter);
        }

        /// <summary>
        /// Decides whether a failed request means that the daily limit is exhausted. The exact status
        /// code is not relied on: for 402, 403 and 429 the (free) token info is asked for the number
        /// of requests left.
        /// </summary>
        private async Task<ApiException> DetectQuotaExhaustion(RawResponse failed, CancellationToken cancellationToken)
        {
            if (failed.StatusCode != 402 && failed.StatusCode != 403 && failed.StatusCode != 429)
                return null;

            TokenInfoDto info = null;
            try
            {
                info = await FetchTokenInfo(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                _logger.LogDebug(e, "Could not get the token info to check the request quota");
            }

            var now = _utcNow();
            DateTimeOffset until;
            if (info != null)
            {
                if (info.RequestsRemaining > 0)
                    return null;

                until = KinopoiskQuota.BlockedUntil(now, KinopoiskQuota.ToResetTime(info.ResetAt), info.Ttl);
            }
            else if (failed.StatusCode == 402)
            {
                until = KinopoiskQuota.BlockedUntil(now, null);
            }
            else
            {
                return null;
            }

            var shouldLog = false;
            lock (_quotaLock)
            {
                if (now >= _quotaBlockedUntil || until > _quotaBlockedUntil)
                {
                    shouldLog = now >= _quotaBlockedUntil;
                    _quotaBlockedUntil = until;
                }
            }

            if (shouldLog)
            {
                _logger.LogWarning(
                    "poiskkino.dev request quota exhausted. Requests are paused until {BlockedUntil:u}.",
                    until);
                _logger.LogDebug("poiskkino.dev response for the exhausted quota: {Response}", failed.Body);
            }

            return new ApiException(
                $"poiskkino.dev daily request limit exceeded. Requests are paused until {until:u}.",
                failed.StatusCode,
                failed.Body,
                isQuotaExceeded: true);
        }

        private async Task WaitForRequestSlot(CancellationToken cancellationToken)
        {
            await _requestStartLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var delay = _nextRequestAt - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                _nextRequestAt = DateTimeOffset.UtcNow.Add(_requestInterval);
            }
            finally
            {
                _requestStartLock.Release();
            }
        }

        /// <summary>The path of a request without query and with the IDs replaced, for log messages.</summary>
        private static string EndpointName(string relativeUrl)
        {
            var queryStart = relativeUrl.IndexOf('?');
            var path = queryStart < 0 ? relativeUrl : relativeUrl.Substring(0, queryStart);
            return IdSegment.Replace(path, "/{id}");
        }

        private sealed class RawResponse
        {
            public RawResponse(int statusCode, string body, TimeSpan? retryAfter)
            {
                StatusCode = statusCode;
                Body = body ?? string.Empty;
                RetryAfter = retryAfter;
            }

            public int StatusCode { get; }
            public string Body { get; }
            public TimeSpan? RetryAfter { get; }
            public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;

            /// <summary>The "message" of an error body like {"message":["..."],"error":"Bad Request","statusCode":400}.</summary>
            public string ErrorMessage
            {
                get
                {
                    if (string.IsNullOrWhiteSpace(Body))
                        return string.Empty;
                    try
                    {
                        var json = Newtonsoft.Json.Linq.JObject.Parse(Body);
                        var message = json["message"];
                        if (message is Newtonsoft.Json.Linq.JArray array)
                            return string.Join("; ", array.Select(item => item.ToString()));
                        return message?.ToString() ?? string.Empty;
                    }
                    catch (Newtonsoft.Json.JsonException)
                    {
                        return string.Empty;
                    }
                }
            }
        }
    }
}
