using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Kinopoisk.MetadataProviders;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Kinopoisk.Tests
{
    public class SeasonEpisodeFailureTests
    {
        private static ApiException QuotaExceeded()
            => new ApiException("quota", 402, string.Empty, new Dictionary<string, IEnumerable<string>>(), null);

        [Fact]
        public async Task SeasonProviderReturnsNoMetadataWhenTheApiFails()
        {
            var provider = new SeasonMetadataProvider(
                new FailingApiClient(QuotaExceeded()),
                new FakeHttpClientFactory(),
                NullLogger<SeasonMetadataProvider>.Instance);
            var info = new SeasonInfo { IndexNumber = 1 };
            info.SeriesProviderIds[Constants.ProviderId] = "123";

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.False(result.HasMetadata);
            Assert.Null(result.Item);
        }

        [Fact]
        public async Task EpisodeProviderReturnsNoMetadataWhenTheApiFails()
        {
            var provider = new EpisodeMetadataProvider(
                new FailingApiClient(new InvalidOperationException("unexpected response")),
                new FakeHttpClientFactory(),
                NullLogger<EpisodeMetadataProvider>.Instance);
            var info = new EpisodeInfo { ParentIndexNumber = 1, IndexNumber = 2 };
            info.SeriesProviderIds[Constants.ProviderId] = "123";

            var result = await provider.GetMetadata(info, CancellationToken.None);

            Assert.False(result.HasMetadata);
            Assert.Null(result.Item);
        }

        [Fact]
        public async Task SeasonProviderStillPropagatesRealCancellation()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var provider = new SeasonMetadataProvider(
                new FailingApiClient(new OperationCanceledException(cancellation.Token)),
                new FakeHttpClientFactory(),
                NullLogger<SeasonMetadataProvider>.Instance);
            var info = new SeasonInfo { IndexNumber = 1 };
            info.SeriesProviderIds[Constants.ProviderId] = "123";

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => provider.GetMetadata(info, cancellation.Token));
        }

        private sealed class FakeHttpClientFactory : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => new HttpClient();
        }

        private sealed class FailingApiClient : IKinopoiskApiClient
        {
            private readonly Exception _exception;

            public FailingApiClient(Exception exception) => _exception = exception;

            public Task<SeasonResponse> GetSeasons(int filmId, CancellationToken? cancellationToken = null)
                => Task.FromException<SeasonResponse>(_exception);

            public Task<PersonResponse> GetPerson(int personId, CancellationToken? cancellationToken = null)
                => throw new NotImplementedException();

            public Task<Film> GetSingleFilm(int filmId, CancellationToken? cancellationToken = null)
                => throw new NotImplementedException();

            public Task<FilmSearchByFiltersResponse> GetFilmByImdbId(string imdbId, CancellationToken? cancellationToken = null)
                => throw new NotImplementedException();

            public Task<ApiKeyInfoResponse> GetApiKeyInfo(string apiKey, CancellationToken? cancellationToken = null)
                => throw new NotImplementedException();

            public Task<DistributionResponse> GetDistributions(int filmId, CancellationToken? cancellationToken = null)
                => throw new NotImplementedException();

            public Task<ICollection<StaffResponse>> GetStaff(int filmId, CancellationToken? cancellationToken = null)
                => throw new NotImplementedException();

            public Task<VideoResponse> GetTrailers(int filmId, CancellationToken? cancellationToken = null)
                => throw new NotImplementedException();

            public Task<FilmSearchResponse> SearchByKeyword(string keyword, int page = 1, CancellationToken? cancellationToken = null)
                => throw new NotImplementedException();
        }
    }
}
