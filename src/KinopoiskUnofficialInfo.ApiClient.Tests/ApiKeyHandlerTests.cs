using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace KinopoiskUnofficialInfo.ApiClient.Tests
{
    public class ApiKeyHandlerTests
    {
        [Fact]
        public async Task UsesLatestApiKeyForEveryRequest()
        {
            var currentKey = "first-key";
            var captureHandler = new HeaderCaptureHandler();
            using var handler = new ApiKeyHandler(() => currentKey)
            {
                InnerHandler = captureHandler
            };
            using var client = new HttpClient(handler);

            await client.GetAsync("https://example.test/first");
            currentKey = "second-key";
            await client.GetAsync("https://example.test/second");

            Assert.Equal(new[] { "first-key", "second-key" }, captureHandler.ApiKeys);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task DoesNotSendRequestWhenApiKeyIsMissing(string apiKey)
        {
            var captureHandler = new HeaderCaptureHandler();
            using var handler = new ApiKeyHandler(() => apiKey)
            {
                InnerHandler = captureHandler
            };
            using var client = new HttpClient(handler);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => client.GetAsync("https://example.test/resource"));

            Assert.Empty(captureHandler.ApiKeys);
        }

        private sealed class HeaderCaptureHandler : HttpMessageHandler
        {
            public System.Collections.Generic.List<string> ApiKeys { get; } = new System.Collections.Generic.List<string>();

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                request.Headers.TryGetValues("X-API-KEY", out var values);
                ApiKeys.Add(values == null ? null : string.Join(",", values));
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
        }
    }
}
