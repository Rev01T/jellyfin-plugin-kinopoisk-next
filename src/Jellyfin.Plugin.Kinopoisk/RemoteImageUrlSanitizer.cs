using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Kinopoisk
{
    /// <summary>
    /// Checks that a remote image exists and is not the Kinopoisk "no poster" placeholder.
    /// </summary>
    /// <remarks>
    /// Providers use <see cref="CreateShared"/>: the number of simultaneous requests is limited for
    /// the whole plugin (Jellyfin refreshes several items at once) and results are cached, because the
    /// same people appear in many films.
    /// </remarks>
    public class RemoteImageUrlSanitizer
    {
        private const int MaxRedirects = 5;
        private const int MaxConcurrentRequests = 6;

        private static readonly SanitizerState SharedState = new SanitizerState(MaxConcurrentRequests, () => DateTimeOffset.UtcNow);

        private readonly HttpClient _httpClient;
        private readonly SanitizerState _state;

        /// <summary>
        /// Creates a sanitizer with its own limit and cache.
        /// </summary>
        public RemoteImageUrlSanitizer(HttpClient httpClient)
            : this(httpClient, new SanitizerState(MaxConcurrentRequests, () => DateTimeOffset.UtcNow))
        {
        }

        internal RemoteImageUrlSanitizer(HttpClient httpClient, SanitizerState state)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>
        /// Creates a sanitizer that shares the request limit and the result cache with all others.
        /// </summary>
        public static RemoteImageUrlSanitizer CreateShared(HttpClient httpClient)
            => new RemoteImageUrlSanitizer(httpClient, SharedState);

        public async Task<string> SanitizeRemoteImageUrl(string url, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url) || url.Contains("no-poster", StringComparison.OrdinalIgnoreCase))
                return null;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var startUri)
                || (startUri.Scheme != Uri.UriSchemeHttp && startUri.Scheme != Uri.UriSchemeHttps))
                return null;

            if (_state.TryGetCached(url, out var cached))
                return cached;

            var check = await CheckAsync(startUri, cancellationToken).ConfigureAwait(false);
            _state.Store(url, check);
            return check.Url;
        }

        private async Task<CheckResult> CheckAsync(Uri startUri, CancellationToken cancellationToken)
        {
            var currentUri = startUri;
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var redirect = 0; redirect <= MaxRedirects; redirect++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!visited.Add(currentUri.AbsoluteUri))
                    return CheckResult.Invalid;

                await _state.Limiter.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
                    HttpResponseMessage response;
                    try
                    {
                        response = await _httpClient.SendAsync(
                            request,
                            HttpCompletionOption.ResponseHeadersRead,
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (HttpRequestException)
                    {
                        return CheckResult.Transient;
                    }
                    catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        return CheckResult.Transient;
                    }

                    using (response)
                    {
                        var status = (int)response.StatusCode;
                        if (status is >= 200 and <= 299)
                            return CheckResult.Valid(currentUri.AbsoluteUri);

                        var location = response.Headers.Location;
                        if (location is null)
                        {
                            // Server errors and throttling say nothing about the image itself.
                            return status >= 500 || status == 429 ? CheckResult.Transient : CheckResult.Invalid;
                        }

                        Uri nextUri;
                        try
                        {
                            nextUri = location.IsAbsoluteUri ? location : new Uri(currentUri, location);
                        }
                        catch (UriFormatException)
                        {
                            return CheckResult.Invalid;
                        }

                        if (nextUri.Scheme != Uri.UriSchemeHttp && nextUri.Scheme != Uri.UriSchemeHttps)
                            return CheckResult.Invalid;

                        if (nextUri.AbsoluteUri.Contains("no-poster", StringComparison.OrdinalIgnoreCase))
                            return CheckResult.Invalid;

                        currentUri = nextUri;
                    }
                }
                finally
                {
                    _state.Limiter.Release();
                }
            }

            return CheckResult.Invalid;
        }

        internal readonly struct CheckResult
        {
            private CheckResult(string url, TimeSpan timeToLive)
            {
                Url = url;
                TimeToLive = timeToLive;
            }

            public string Url { get; }

            public TimeSpan TimeToLive { get; }

            public static CheckResult Valid(string url) => new CheckResult(url, TimeSpan.FromHours(24));

            // The image is missing or a placeholder: this does not change within hours.
            public static CheckResult Invalid => new CheckResult(null, TimeSpan.FromHours(6));

            // Network trouble: remember it only briefly to avoid hammering the server.
            public static CheckResult Transient => new CheckResult(null, TimeSpan.FromMinutes(1));
        }

        /// <summary>
        /// The request limit and the result cache shared by sanitizers.
        /// </summary>
        internal sealed class SanitizerState
        {
            private const int MaxCacheEntries = 20000;

            private readonly ConcurrentDictionary<string, CacheEntry> _cache = new ConcurrentDictionary<string, CacheEntry>(StringComparer.Ordinal);
            private readonly Func<DateTimeOffset> _utcNow;

            public SanitizerState(int maxConcurrentRequests, Func<DateTimeOffset> utcNow)
            {
                Limiter = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
                _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            }

            public SemaphoreSlim Limiter { get; }

            public bool TryGetCached(string url, out string sanitizedUrl)
            {
                if (_cache.TryGetValue(url, out var entry))
                {
                    if (entry.ExpiresAt > _utcNow())
                    {
                        sanitizedUrl = entry.Url;
                        return true;
                    }

                    _cache.TryRemove(url, out _);
                }

                sanitizedUrl = null;
                return false;
            }

            public void Store(string url, CheckResult result)
            {
                if (_cache.Count >= MaxCacheEntries)
                    Trim();

                _cache[url] = new CacheEntry(result.Url, _utcNow() + result.TimeToLive);
            }

            private void Trim()
            {
                var now = _utcNow();
                foreach (var expired in _cache.Where(pair => pair.Value.ExpiresAt <= now).Select(pair => pair.Key).ToList())
                    _cache.TryRemove(expired, out _);

                // Still full: drop everything, results are cheap to rebuild.
                if (_cache.Count >= MaxCacheEntries)
                    _cache.Clear();
            }

            private sealed class CacheEntry
            {
                public CacheEntry(string url, DateTimeOffset expiresAt)
                {
                    Url = url;
                    ExpiresAt = expiresAt;
                }

                public string Url { get; }

                public DateTimeOffset ExpiresAt { get; }
            }
        }
    }
}
