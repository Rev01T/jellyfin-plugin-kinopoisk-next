using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Jellyfin.Plugin.Kinopoisk.Tests
{
    public class RemoteImageUrlSanitizerTests
    {
        [Fact]
        public async Task SanitizeRemoteImageUrlDisposesRequestAndResponse()
        {
            using var handler = new TrackingHandler();
            using var httpClient = new HttpClient(handler);

            var sanitizer = new RemoteImageUrlSanitizer(httpClient);

            var result = await sanitizer.SanitizeRemoteImageUrl(
                "https://example.com/image.jpg");

            Assert.Equal("https://example.com/image.jpg", result);
            Assert.True(handler.RequestContent.IsDisposed);
            Assert.True(handler.ResponseContent.IsDisposed);
        }

        [Fact]
        public async Task SanitizeRemoteImageUrlCachesValidResultsAcrossInstances()
        {
            var clock = new FakeClock();
            var state = new RemoteImageUrlSanitizer.SanitizerState(4, clock.UtcNow);
            var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            using var httpClient = new HttpClient(handler);

            var first = await new RemoteImageUrlSanitizer(httpClient, state).SanitizeRemoteImageUrl("https://example.com/a.jpg");
            var second = await new RemoteImageUrlSanitizer(httpClient, state).SanitizeRemoteImageUrl("https://example.com/a.jpg");

            Assert.Equal("https://example.com/a.jpg", first);
            Assert.Equal("https://example.com/a.jpg", second);
            Assert.Equal(1, handler.RequestCount);
        }

        [Fact]
        public async Task SanitizeRemoteImageUrlCachesMissingImages()
        {
            var clock = new FakeClock();
            var state = new RemoteImageUrlSanitizer.SanitizerState(4, clock.UtcNow);
            var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
            using var httpClient = new HttpClient(handler);
            var sanitizer = new RemoteImageUrlSanitizer(httpClient, state);

            Assert.Null(await sanitizer.SanitizeRemoteImageUrl("https://example.com/missing.jpg"));
            Assert.Null(await sanitizer.SanitizeRemoteImageUrl("https://example.com/missing.jpg"));

            Assert.Equal(1, handler.RequestCount);
        }

        [Fact]
        public async Task SanitizeRemoteImageUrlRefetchesAfterTheCacheEntryExpires()
        {
            var clock = new FakeClock();
            var state = new RemoteImageUrlSanitizer.SanitizerState(4, clock.UtcNow);
            var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
            using var httpClient = new HttpClient(handler);
            var sanitizer = new RemoteImageUrlSanitizer(httpClient, state);

            await sanitizer.SanitizeRemoteImageUrl("https://example.com/a.jpg");
            clock.Now += TimeSpan.FromHours(23);
            await sanitizer.SanitizeRemoteImageUrl("https://example.com/a.jpg");
            Assert.Equal(1, handler.RequestCount);

            clock.Now += TimeSpan.FromHours(2);
            await sanitizer.SanitizeRemoteImageUrl("https://example.com/a.jpg");
            Assert.Equal(2, handler.RequestCount);
        }

        [Fact]
        public async Task SanitizeRemoteImageUrlRemembersNetworkFailuresOnlyBriefly()
        {
            var clock = new FakeClock();
            var state = new RemoteImageUrlSanitizer.SanitizerState(4, clock.UtcNow);
            var fail = true;
            var handler = new CountingHandler(_ => fail
                ? throw new HttpRequestException("connection refused")
                : new HttpResponseMessage(HttpStatusCode.OK));
            using var httpClient = new HttpClient(handler);
            var sanitizer = new RemoteImageUrlSanitizer(httpClient, state);

            Assert.Null(await sanitizer.SanitizeRemoteImageUrl("https://example.com/a.jpg"));
            Assert.Null(await sanitizer.SanitizeRemoteImageUrl("https://example.com/a.jpg"));
            Assert.Equal(1, handler.RequestCount);

            fail = false;
            clock.Now += TimeSpan.FromMinutes(2);
            Assert.Equal("https://example.com/a.jpg", await sanitizer.SanitizeRemoteImageUrl("https://example.com/a.jpg"));
            Assert.Equal(2, handler.RequestCount);
        }

        [Fact]
        public async Task SanitizeRemoteImageUrlDoesNotTreatServerErrorsAsMissingImages()
        {
            var clock = new FakeClock();
            var state = new RemoteImageUrlSanitizer.SanitizerState(4, clock.UtcNow);
            var status = HttpStatusCode.ServiceUnavailable;
            var handler = new CountingHandler(_ => new HttpResponseMessage(status));
            using var httpClient = new HttpClient(handler);
            var sanitizer = new RemoteImageUrlSanitizer(httpClient, state);

            Assert.Null(await sanitizer.SanitizeRemoteImageUrl("https://example.com/a.jpg"));

            status = HttpStatusCode.OK;
            clock.Now += TimeSpan.FromMinutes(2);
            Assert.Equal("https://example.com/a.jpg", await sanitizer.SanitizeRemoteImageUrl("https://example.com/a.jpg"));
        }

        [Fact]
        public async Task SanitizeRemoteImageUrlTreatsRedirectToPlaceholderAsMissing()
        {
            var clock = new FakeClock();
            var state = new RemoteImageUrlSanitizer.SanitizerState(4, clock.UtcNow);
            var handler = new CountingHandler(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.Found);
                response.Headers.Location = new Uri("https://example.com/no-poster.png");
                return response;
            });
            using var httpClient = new HttpClient(handler);

            Assert.Null(await new RemoteImageUrlSanitizer(httpClient, state).SanitizeRemoteImageUrl("https://example.com/a.jpg"));
            Assert.Equal(1, handler.RequestCount);
        }

        [Fact]
        public async Task SanitizeRemoteImageUrlLimitsSimultaneousRequests()
        {
            var clock = new FakeClock();
            var state = new RemoteImageUrlSanitizer.SanitizerState(2, clock.UtcNow);
            var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK), delay: TimeSpan.FromMilliseconds(20));
            using var httpClient = new HttpClient(handler);
            var sanitizer = new RemoteImageUrlSanitizer(httpClient, state);

            var tasks = new List<Task<string>>();
            for (var i = 0; i < 12; i++)
                tasks.Add(sanitizer.SanitizeRemoteImageUrl($"https://example.com/{i}.jpg"));
            var results = await Task.WhenAll(tasks);

            Assert.Equal(12, handler.RequestCount);
            Assert.True(handler.MaxConcurrentRequests <= 2, $"Observed {handler.MaxConcurrentRequests} simultaneous requests");
            Assert.True(handler.MaxConcurrentRequests >= 1);
            Assert.Equal(12, results.Length);
            Assert.Equal("https://example.com/11.jpg", results[11]);
        }

        private sealed class FakeClock
        {
            public DateTimeOffset Now { get; set; } = new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

            public DateTimeOffset UtcNow() => Now;
        }

        private sealed class CountingHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
            private readonly TimeSpan _delay;
            private int _requestCount;
            private int _concurrent;
            private int _maxConcurrent;

            public CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond, TimeSpan? delay = null)
            {
                _respond = respond;
                _delay = delay ?? TimeSpan.Zero;
            }

            public int RequestCount => Volatile.Read(ref _requestCount);

            public int MaxConcurrentRequests => Volatile.Read(ref _maxConcurrent);

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _requestCount);
                var concurrent = Interlocked.Increment(ref _concurrent);
                int seen;
                while (concurrent > (seen = Volatile.Read(ref _maxConcurrent)))
                    Interlocked.CompareExchange(ref _maxConcurrent, concurrent, seen);

                try
                {
                    if (_delay > TimeSpan.Zero)
                        await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);

                    return _respond(request);
                }
                finally
                {
                    Interlocked.Decrement(ref _concurrent);
                }
            }
        }

        private sealed class TrackingHandler : HttpMessageHandler
        {
            public TrackingContent RequestContent { get; } = new TrackingContent();

            public TrackingContent ResponseContent { get; } = new TrackingContent();

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                request.Content = RequestContent;

                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = ResponseContent
                    });
            }
        }

        private sealed class TrackingContent : HttpContent
        {
            public bool IsDisposed { get; private set; }

            protected override Task SerializeToStreamAsync(
                Stream stream,
                TransportContext context)
            {
                return Task.CompletedTask;
            }

            protected override bool TryComputeLength(out long length)
            {
                length = 0;
                return true;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    IsDisposed = true;
                }

                base.Dispose(disposing);
            }
        }
    }
}
