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
            => new ApiException("quota", 402, string.Empty, isQuotaExceeded: true);

        [Fact]
        public async Task SeasonProviderReturnsNoMetadataWhenTheApiFails()
        {
            var provider = new SeasonMetadataProvider(
                new FakeApiClient { Failure = QuotaExceeded() },
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
                new FakeApiClient { Failure = new InvalidOperationException("unexpected response") },
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
                new FakeApiClient { Failure = new OperationCanceledException(cancellation.Token) },
                new FakeHttpClientFactory(),
                NullLogger<SeasonMetadataProvider>.Instance);
            var info = new SeasonInfo { IndexNumber = 1 };
            info.SeriesProviderIds[Constants.ProviderId] = "123";

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => provider.GetMetadata(info, cancellation.Token));
        }
    }
}
