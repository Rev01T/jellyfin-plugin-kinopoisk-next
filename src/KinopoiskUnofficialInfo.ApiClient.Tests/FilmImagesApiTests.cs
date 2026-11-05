using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KinopoiskUnofficialInfo.ApiClient.Tests
{
    public class FilmImagesApiTests
    {
        [Fact]
        public async Task GetImagesRequestsSelectedTypeAndParsesOriginalAndPreviewUrls()
        {
            var handler = new StubHttpMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"total\":1,\"totalPages\":1,\"items\":[{\"imageUrl\":\"https://images.example/orig\",\"previewUrl\":\"https://images.example/300x\"}]}")
                });
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://kinopoiskapiunofficial.tech")
            };
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(clientFactory => clientFactory.CreateClient("KinopoiskApiClient")).Returns(httpClient);
            var client = new KinopoiskApiClient("test-key", NullLogger<KinopoiskApiClient>.Instance, factory.Object);

            var result = await client.GetImages(123, "POSTER", 2);

            Assert.Equal("/api/v2.2/films/123/images?type=POSTER&page=2", handler.LastRequestUri.PathAndQuery);
            Assert.Equal(1, result.Total);
            Assert.Equal(1, result.TotalPages);
            Assert.Single(result.Items);
            Assert.Equal("https://images.example/orig", result.Items[0].ImageUrl);
            Assert.Equal("https://images.example/300x", result.Items[0].PreviewUrl);
        }

        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

            public Uri LastRequestUri { get; private set; }

            public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
            {
                _responseFactory = responseFactory;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                LastRequestUri = request.RequestUri;
                return Task.FromResult(_responseFactory(request));
            }
        }
    }
}
