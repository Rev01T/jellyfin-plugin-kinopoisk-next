using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KinopoiskUnofficialInfo.ApiClient.Tests
{
    /// <summary>
    /// The free token has a daily limit of requests. The API's answer to an exhausted limit is not
    /// documented, so the client asks the token info (which is free) instead of trusting the status code.
    /// </summary>
    public class QuotaTests
    {
        private const string TokenWithRequestsLeft = "{\"requestsLimit\":200,\"requestsUsed\":14,\"requestsRemaining\":186,\"ttl\":42779,\"resetAt\":\"2026-11-20T20:59:59.723Z\"}";
        private const string TokenExhausted = "{\"requestsLimit\":200,\"requestsUsed\":200,\"requestsRemaining\":0,\"ttl\":32399,\"resetAt\":\"2026-11-20T20:59:59.723Z\"}";
        private const string Movie = "{\"id\":301,\"name\":\"Матрица\"}";

        private static readonly DateTimeOffset Noon = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

        private static KinopoiskApiClient CreateClient(RoutingHandler handler, Func<DateTimeOffset> clock)
            => new KinopoiskApiClient(
                "test-token",
                NullLogger<KinopoiskApiClient>.Instance,
                new SingleHandlerClientFactory(handler),
                clock,
                requestInterval: TimeSpan.Zero);

        [Fact]
        public async Task ExhaustedQuota_PausesRequestsUntilTheReset()
        {
            var now = Noon;
            var exhausted = true;
            var handler = new RoutingHandler(target =>
                target == "/v1.5/token" ? FakeResponse.Ok(exhausted ? TokenExhausted : TokenWithRequestsLeft)
                : exhausted ? FakeResponse.Error(403, "{\"message\":\"Daily limit\"}")
                : FakeResponse.Ok(Movie));
            var client = CreateClient(handler, () => now);

            var first = await Assert.ThrowsAsync<ApiException>(() => client.GetMovie(301));
            var second = await Assert.ThrowsAsync<ApiException>(() => client.GetMovie(302));

            Assert.True(first.IsQuotaExceeded);
            Assert.Equal(403, first.StatusCode);
            Assert.True(second.IsQuotaExceeded);
            Assert.Contains("2026-11-20 21:00:04Z", second.Message);
            // The second call did not reach the API.
            Assert.Equal(1, handler.Count("/v1.5/movie/301"));
            Assert.Equal(0, handler.Count("/v1.5/movie/302"));
            Assert.Equal(1, handler.Count("/v1.5/token"));

            // The token info stays available during the pause.
            Assert.Equal(0, (await client.GetTokenInfo()).RequestsRemaining);

            // Still paused one second before the reset...
            now = new DateTimeOffset(2026, 11, 20, 20, 59, 59, TimeSpan.Zero);
            await Assert.ThrowsAsync<ApiException>(() => client.GetMovie(303));
            Assert.Equal(0, handler.Count("/v1.5/movie/303"));

            // ...and working again right after it.
            exhausted = false;
            now = new DateTimeOffset(2026, 11, 20, 21, 0, 10, TimeSpan.Zero);
            Assert.Equal("Матрица", (await client.GetMovie(304)).Name);
        }

        [Fact]
        public async Task QuotaStillReportedExhaustedAfterTheReset_PausesBrieflyOnly()
        {
            var now = new DateTimeOffset(2026, 11, 20, 21, 0, 30, TimeSpan.Zero);
            var handler = new RoutingHandler(target =>
                target == "/v1.5/token" ? FakeResponse.Ok(TokenExhausted) : FakeResponse.Error(403));
            var client = CreateClient(handler, () => now);

            var exception = await Assert.ThrowsAsync<ApiException>(() => client.GetMovie(301));

            Assert.True(exception.IsQuotaExceeded);
            Assert.Contains("2026-11-20 21:01:30Z", exception.Message);
        }

        [Fact]
        public async Task TooManyRequests_IsRetriedOnceWhenTheQuotaIsNotExhausted()
        {
            var attempts = 0;
            var handler = new RoutingHandler(target =>
            {
                if (target == "/v1.5/token")
                    return FakeResponse.Ok(TokenWithRequestsLeft);
                return ++attempts == 1
                    ? new FakeResponse { Status = (System.Net.HttpStatusCode)429, RetryAfter = TimeSpan.Zero }
                    : FakeResponse.Ok(Movie);
            });
            var client = CreateClient(handler, () => Noon);

            var movie = await client.GetMovie(301);

            Assert.Equal("Матрица", movie.Name);
            Assert.Equal(2, handler.Count("/v1.5/movie/301"));
        }

        [Fact]
        public async Task TooManyRequestsTwice_IsAnOrdinaryErrorAndDoesNotPause()
        {
            var handler = new RoutingHandler(target =>
                target == "/v1.5/token"
                    ? FakeResponse.Ok(TokenWithRequestsLeft)
                    : new FakeResponse { Status = (System.Net.HttpStatusCode)429, RetryAfter = TimeSpan.Zero });
            var client = CreateClient(handler, () => Noon);

            var exception = await Assert.ThrowsAsync<ApiException>(() => client.GetMovie(301));
            await Assert.ThrowsAsync<ApiException>(() => client.GetMovie(302));

            Assert.False(exception.IsQuotaExceeded);
            Assert.Equal(429, exception.StatusCode);
            // The second title was requested: there was no pause.
            Assert.Equal(2, handler.Count("/v1.5/movie/302"));
        }

        [Fact]
        public async Task Forbidden_WithRequestsLeft_IsAnOrdinaryError()
        {
            var handler = new RoutingHandler(target =>
                target == "/v1.5/token" ? FakeResponse.Ok(TokenWithRequestsLeft) : FakeResponse.Error(403, "{\"message\":\"Forbidden\"}"));
            var client = CreateClient(handler, () => Noon);

            var exception = await Assert.ThrowsAsync<ApiException>(() => client.GetMovie(301));
            await Assert.ThrowsAsync<ApiException>(() => client.GetMovie(302));

            Assert.False(exception.IsQuotaExceeded);
            Assert.Equal(403, exception.StatusCode);
            Assert.Equal(1, handler.Count("/v1.5/movie/302"));
        }

        [Fact]
        public async Task Forbidden_WhenTheTokenInfoIsForbiddenToo_IsAnOrdinaryError()
        {
            // A wrong or disabled token: nothing to wait for.
            var handler = new RoutingHandler(_ => FakeResponse.Error(403));
            var client = CreateClient(handler, () => Noon);

            var exception = await Assert.ThrowsAsync<ApiException>(() => client.GetMovie(301));

            Assert.False(exception.IsQuotaExceeded);
            Assert.Equal(403, exception.StatusCode);
        }

        [Fact]
        public async Task PaymentRequired_WhenTheTokenInfoFails_PausesForAnHour()
        {
            var now = Noon;
            var handler = new RoutingHandler(_ => FakeResponse.Error(402));
            var client = CreateClient(handler, () => now);

            var exception = await Assert.ThrowsAsync<ApiException>(() => client.GetMovie(301));
            Assert.True(exception.IsQuotaExceeded);

            now = Noon.AddMinutes(59);
            await Assert.ThrowsAsync<ApiException>(() => client.GetMovie(302));
            Assert.Equal(0, handler.Count("/v1.5/movie/302"));

            now = Noon.AddMinutes(61);
            await Assert.ThrowsAsync<ApiException>(() => client.GetMovie(303));
            Assert.Equal(1, handler.Count("/v1.5/movie/303"));
        }

        [Fact]
        public async Task Unauthorized_IsAnOrdinaryError()
        {
            var handler = new RoutingHandler(_ => FakeResponse.Error(401, "{\"message\":\"Unauthorized\"}"));
            var client = CreateClient(handler, () => Noon);

            var exception = await Assert.ThrowsAsync<ApiException>(() => client.GetMovie(301));

            Assert.False(exception.IsQuotaExceeded);
            Assert.Equal(401, exception.StatusCode);
            // No token info request: 401 is not a quota error.
            Assert.Equal(0, handler.Count("/v1.5/token"));
        }

        [Fact]
        public async Task CachedClient_DoesNotCacheAQuotaError()
        {
            var handler = new RoutingHandler(target =>
                target == "/v1.5/token" ? FakeResponse.Ok(TokenExhausted) : FakeResponse.Error(403));
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var client = new CachedKinopoiskApiClient(
                CreateClient(handler, () => Noon),
                cache,
                NullLogger<CachedKinopoiskApiClient>.Instance);

            await Assert.ThrowsAsync<ApiException>(() => client.GetMovie(1000));
            await Assert.ThrowsAsync<ApiException>(() => client.GetMovie(1000));
            await Assert.ThrowsAsync<ApiException>(() => client.GetMovie(2000));

            // Only the first request reaches the API; the others fail from the client's pause,
            // so no empty fallback was stored in the response cache.
            Assert.Equal(1, handler.Count("/v1.5/movie/1000"));
            Assert.Equal(0, handler.Count("/v1.5/movie/2000"));
        }

        [Fact]
        public async Task CachedClient_CachesTitlesAndMissingTitles()
        {
            var handler = new RoutingHandler(target =>
                target == "/v1.5/movie/301" ? FakeResponse.Ok(Movie) : FakeResponse.Error(400, "{\"message\":[\"id\"]}"));
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var client = new CachedKinopoiskApiClient(
                CreateClient(handler, () => Noon),
                cache,
                NullLogger<CachedKinopoiskApiClient>.Instance);

            await client.GetMovie(301);
            await client.GetMovie(301);
            Assert.Null(await client.GetMovie(5));
            Assert.Null(await client.GetMovie(5));

            Assert.Equal(1, handler.Count("/v1.5/movie/301"));
            Assert.Equal(1, handler.Count("/v1.5/movie/5"));
        }
    }

    public class KinopoiskQuotaTests
    {
        private static readonly DateTimeOffset Noon = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public void PauseLastsUntilTheAnnouncedReset()
        {
            var resetAt = new DateTimeOffset(2026, 11, 20, 20, 59, 59, 723, TimeSpan.Zero);

            var until = KinopoiskQuota.BlockedUntil(Noon, resetAt, 32399);

            Assert.Equal(resetAt + KinopoiskQuota.ResetMargin, until);
        }

        [Fact]
        public void PauseIsShortWhenTheAnnouncedResetHasAlreadyPassed()
        {
            var until = KinopoiskQuota.BlockedUntil(Noon, Noon.AddMinutes(-5));

            Assert.Equal(Noon + KinopoiskQuota.GraceCooldown, until);
        }

        [Fact]
        public void PauseUsesTheTtlWhenThereIsNoResetTime()
        {
            var until = KinopoiskQuota.BlockedUntil(Noon, null, 600);

            Assert.Equal(Noon.AddSeconds(600) + KinopoiskQuota.ResetMargin, until);
        }

        [Fact]
        public void PauseIsAnHourWhenNothingIsKnown()
        {
            Assert.Equal(Noon.AddHours(1), KinopoiskQuota.BlockedUntil(Noon, null));
            Assert.Equal(Noon.AddHours(1), KinopoiskQuota.BlockedUntil(Noon, null, 0));
        }

        [Fact]
        public void PauseIsNeverLongerThanTheLimit()
        {
            var until = KinopoiskQuota.BlockedUntil(Noon, Noon.AddDays(30));

            Assert.Equal(Noon + KinopoiskQuota.MaxCooldown, until);
        }

        [Fact]
        public void ResetTimeIsReadAsUtc()
        {
            var utc = new DateTime(2026, 11, 20, 20, 59, 59, DateTimeKind.Utc);
            var unspecified = new DateTime(2026, 11, 20, 20, 59, 59, DateTimeKind.Unspecified);

            Assert.Null(KinopoiskQuota.ToResetTime(null));
            Assert.Equal(new DateTimeOffset(2026, 11, 20, 20, 59, 59, TimeSpan.Zero), KinopoiskQuota.ToResetTime(utc));
            Assert.Equal(new DateTimeOffset(2026, 11, 20, 20, 59, 59, TimeSpan.Zero), KinopoiskQuota.ToResetTime(unspecified));
        }
    }
}
