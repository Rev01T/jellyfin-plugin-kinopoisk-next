using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KinopoiskUnofficialInfo.ApiClient.Tests
{
    public class QuotaResetTests
    {
        private static readonly TimeSpan Zero = TimeSpan.Zero;

        [Fact]
        public void NextResetIsThreeAmMoscowTime()
        {
            // 23:40 UTC on the 15th is 02:40 on the 16th in Moscow.
            var next = KinopoiskQuota.NextReset(new DateTimeOffset(2026, 11, 15, 23, 40, 0, Zero));

            Assert.Equal(new DateTimeOffset(2026, 11, 16, 0, 0, 0, Zero), next);
            Assert.Equal(new DateTimeOffset(2026, 11, 16, 3, 0, 0, TimeSpan.FromHours(3)), next);
        }

        [Fact]
        public void ResetMomentBelongsToTheNewQuotaDay()
        {
            var reset = new DateTimeOffset(2026, 11, 16, 0, 0, 0, Zero);

            Assert.Equal(reset, KinopoiskQuota.PreviousReset(reset));
            Assert.Equal(new DateTimeOffset(2026, 11, 17, 0, 0, 0, Zero), KinopoiskQuota.NextReset(reset));
            Assert.Equal(reset, KinopoiskQuota.PreviousReset(reset.AddSeconds(1)));
            Assert.Equal(reset.AddDays(-1), KinopoiskQuota.PreviousReset(reset.AddSeconds(-1)));
        }

        [Fact]
        public void PauseUsesTheCooldownInTheMiddleOfTheDay()
        {
            var now = new DateTimeOffset(2026, 11, 15, 12, 0, 0, Zero);

            Assert.Equal(now.AddHours(1), KinopoiskQuota.BlockedUntil(now));
        }

        [Fact]
        public void PauseNeverExtendsPastTheNextReset()
        {
            var now = new DateTimeOffset(2026, 11, 15, 23, 40, 0, Zero);

            Assert.Equal(new DateTimeOffset(2026, 11, 16, 0, 0, 0, Zero), KinopoiskQuota.BlockedUntil(now));
        }

        [Fact]
        public void PauseIsShortRightAfterTheReset()
        {
            var now = new DateTimeOffset(2026, 11, 16, 0, 2, 0, Zero);

            Assert.Equal(now.AddMinutes(1), KinopoiskQuota.BlockedUntil(now));
        }

        [Fact]
        public async Task RequestsResumeAtTheDailyResetInsteadOfAfterAFixedHour()
        {
            var clock = new FakeClock(new DateTimeOffset(2026, 11, 15, 23, 40, 0, Zero));
            var handler = new SwitchableHandler { QuotaExceeded = true };
            var client = CreateClient(handler, clock);

            // 02:40 Moscow time: the quota is exhausted.
            await Assert.ThrowsAsync<ApiException>(() => client.GetSingleFilm(1));
            Assert.Equal(1, handler.RequestCount);

            // Still before the reset: no request reaches the API.
            clock.Now = new DateTimeOffset(2026, 11, 15, 23, 59, 59, Zero);
            await Assert.ThrowsAsync<ApiException>(() => client.GetSingleFilm(1));
            Assert.Equal(1, handler.RequestCount);

            // The API has reset the counter at 03:00 Moscow time.
            handler.QuotaExceeded = false;
            clock.Now = new DateTimeOffset(2026, 11, 16, 0, 0, 1, Zero);
            var film = await client.GetSingleFilm(1);

            Assert.Equal(2, handler.RequestCount);
            Assert.Equal("Тест", film.NameRu);
        }

        [Fact]
        public async Task RequestsAreRetriedQuicklyWhenTheApiLagsBehindTheReset()
        {
            var clock = new FakeClock(new DateTimeOffset(2026, 11, 16, 0, 2, 0, Zero));
            var handler = new SwitchableHandler { QuotaExceeded = true };
            var client = CreateClient(handler, clock);

            await Assert.ThrowsAsync<ApiException>(() => client.GetSingleFilm(1));
            Assert.Equal(1, handler.RequestCount);

            clock.Now = new DateTimeOffset(2026, 11, 16, 0, 2, 30, Zero);
            await Assert.ThrowsAsync<ApiException>(() => client.GetSingleFilm(1));
            Assert.Equal(1, handler.RequestCount);

            handler.QuotaExceeded = false;
            clock.Now = new DateTimeOffset(2026, 11, 16, 0, 3, 1, Zero);
            var film = await client.GetSingleFilm(1);

            Assert.Equal(2, handler.RequestCount);
            Assert.Equal("Тест", film.NameRu);
        }

        [Fact]
        public async Task PausedRequestsDoNotReplayTheStaleQuotaResponse()
        {
            var clock = new FakeClock(new DateTimeOffset(2026, 11, 15, 12, 0, 0, Zero));
            var handler = new SwitchableHandler { QuotaExceeded = true };
            var client = CreateClient(handler, clock);

            await Assert.ThrowsAsync<ApiException>(() => client.GetSingleFilm(1));
            var paused = await Assert.ThrowsAsync<ApiException>(() => client.GetSingleFilm(1));

            Assert.Equal(402, paused.StatusCode);
            Assert.Equal(string.Empty, paused.Response);
            Assert.Contains("paused until", paused.Message);
            Assert.Equal(1, handler.RequestCount);
        }

        private static KinopoiskApiClient CreateClient(SwitchableHandler handler, FakeClock clock)
            => new KinopoiskApiClient(
                "test-token",
                NullLogger<KinopoiskApiClient>.Instance,
                new FakeHttpClientFactory(handler),
                clock.UtcNow);

        private sealed class FakeClock
        {
            public FakeClock(DateTimeOffset now) => Now = now;

            public DateTimeOffset Now { get; set; }

            public DateTimeOffset UtcNow() => Now;
        }

        private sealed class FakeHttpClientFactory : IHttpClientFactory
        {
            private readonly HttpMessageHandler _handler;

            public FakeHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

            public HttpClient CreateClient(string name) => new HttpClient(_handler, disposeHandler: false);
        }

        private sealed class SwitchableHandler : HttpMessageHandler
        {
            private int _requestCount;

            public bool QuotaExceeded { get; set; }

            public int RequestCount => _requestCount;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _requestCount);
                if (QuotaExceeded)
                {
                    return Task.FromResult(new HttpResponseMessage((HttpStatusCode)402)
                    {
                        Content = new StringContent("{\"message\":\"Daily request limit exceeded: 500/500\"}")
                    });
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"kinopoiskId\":1,\"nameRu\":\"Тест\"}", Encoding.UTF8, "application/json")
                });
            }
        }
    }
}
