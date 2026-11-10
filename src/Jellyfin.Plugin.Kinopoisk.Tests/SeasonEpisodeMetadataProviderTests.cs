using System.Collections.Generic;
using Jellyfin.Plugin.Kinopoisk.MetadataProviders;
using KinopoiskUnofficialInfo.ApiClient;
using Xunit;

namespace Jellyfin.Plugin.Kinopoisk.Tests
{
    public class SeasonEpisodeMetadataProviderTests
    {
        [Fact]
        public void FindSeasonMatchesRequestedSeasonNumber()
        {
            var expected = new KinopoiskUnofficialInfo.ApiClient.Season { Number = 2 };
            var response = new SeasonResponse
            {
                Items = new List<KinopoiskUnofficialInfo.ApiClient.Season>
                {
                    null,
                    new KinopoiskUnofficialInfo.ApiClient.Season { Number = 1 },
                    expected
                }
            };

            Assert.Same(expected, SeasonMetadataProvider.FindSeason(response, 2));
        }

        [Fact]
        public void FindSeasonReturnsNullForMissingSeasonOrResponse()
        {
            var response = new SeasonResponse
            {
                Items = new List<KinopoiskUnofficialInfo.ApiClient.Season>
                {
                    new KinopoiskUnofficialInfo.ApiClient.Season { Number = 1 }
                }
            };

            Assert.Null(SeasonMetadataProvider.FindSeason(response, 3));
            Assert.Null(SeasonMetadataProvider.FindSeason(null, 1));
            Assert.Null(SeasonMetadataProvider.FindSeason(new SeasonResponse(), 1));
        }

        [Fact]
        public void FindEpisodeMatchesSeasonAndEpisodeNumbers()
        {
            var expected = new KinopoiskUnofficialInfo.ApiClient.Episode
            {
                SeasonNumber = 2,
                EpisodeNumber = 3
            };
            var response = new SeasonResponse
            {
                Items = new List<KinopoiskUnofficialInfo.ApiClient.Season>
                {
                    new KinopoiskUnofficialInfo.ApiClient.Season
                    {
                        Number = 1,
                        Episodes = new List<KinopoiskUnofficialInfo.ApiClient.Episode>
                        {
                            new KinopoiskUnofficialInfo.ApiClient.Episode { SeasonNumber = 1, EpisodeNumber = 3 }
                        }
                    },
                    new KinopoiskUnofficialInfo.ApiClient.Season
                    {
                        Number = 2,
                        Episodes = new List<KinopoiskUnofficialInfo.ApiClient.Episode>
                        {
                            null,
                            new KinopoiskUnofficialInfo.ApiClient.Episode { SeasonNumber = 2, EpisodeNumber = 2 },
                            expected
                        }
                    }
                }
            };

            Assert.Same(expected, EpisodeMetadataProvider.FindEpisode(response, 2, 3));
        }

        [Fact]
        public void FindEpisodeReturnsNullForMissingEpisodeOrResponse()
        {
            var response = new SeasonResponse
            {
                Items = new List<KinopoiskUnofficialInfo.ApiClient.Season>
                {
                    new KinopoiskUnofficialInfo.ApiClient.Season { Number = 1, Episodes = null }
                }
            };

            Assert.Null(EpisodeMetadataProvider.FindEpisode(response, 1, 1));
            Assert.Null(EpisodeMetadataProvider.FindEpisode(null, 1, 1));
            Assert.Null(EpisodeMetadataProvider.FindEpisode(new SeasonResponse(), 1, 1));
        }

        [Fact]
        public void TryGetSeriesIdRejectsMissingInvalidAndNonPositiveIds()
        {
            Assert.False(SeasonMetadataProvider.TryGetSeriesId(null, out _));
            Assert.False(SeasonMetadataProvider.TryGetSeriesId(new Dictionary<string, string>(), out _));
            Assert.False(SeasonMetadataProvider.TryGetSeriesId(
                new Dictionary<string, string> { [Constants.ProviderId] = "abc" }, out _));
            Assert.False(SeasonMetadataProvider.TryGetSeriesId(
                new Dictionary<string, string> { [Constants.ProviderId] = "0" }, out _));
        }

        [Fact]
        public void TryGetSeriesIdAcceptsPositiveInvariantInteger()
        {
            Assert.True(SeasonMetadataProvider.TryGetSeriesId(
                new Dictionary<string, string> { [Constants.ProviderId] = "12345" }, out var id));
            Assert.Equal(12345, id);
        }
    }
}