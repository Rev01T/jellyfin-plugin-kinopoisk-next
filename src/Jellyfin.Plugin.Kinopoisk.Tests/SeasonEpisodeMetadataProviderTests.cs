using System.Collections.Generic;
using System.Linq;
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
            var expected = new SeasonDto { Number = 2 };
            var seasons = new List<SeasonDto> { null, new SeasonDto { Number = 1 }, expected };

            Assert.Same(expected, SeasonMetadataProvider.FindSeason(seasons, 2));
        }

        [Fact]
        public void FindSeasonReturnsNullForMissingSeasonOrResponse()
        {
            var seasons = new List<SeasonDto> { new SeasonDto { Number = 1 } };

            Assert.Null(SeasonMetadataProvider.FindSeason(seasons, 3));
            Assert.Null(SeasonMetadataProvider.FindSeason(null, 1));
            Assert.Null(SeasonMetadataProvider.FindSeason(new List<SeasonDto>(), 1));
        }

        [Fact]
        public void FindEpisodeMatchesSeasonAndEpisodeNumbers()
        {
            var expected = new EpisodeDto { Number = 3 };
            var seasons = new List<SeasonDto>
            {
                new SeasonDto { Number = 1, Episodes = new List<EpisodeDto> { new EpisodeDto { Number = 3 } } },
                new SeasonDto { Number = 2, Episodes = new List<EpisodeDto> { null, new EpisodeDto { Number = 2 }, expected } }
            };

            Assert.Same(expected, EpisodeMetadataProvider.FindEpisode(seasons, 2, 3));
        }

        [Fact]
        public void FindEpisodeReturnsNullForMissingEpisodeOrResponse()
        {
            var seasons = new List<SeasonDto> { new SeasonDto { Number = 1, Episodes = null } };

            Assert.Null(EpisodeMetadataProvider.FindEpisode(seasons, 1, 1));
            Assert.Null(EpisodeMetadataProvider.FindEpisode(null, 1, 1));
            Assert.Null(EpisodeMetadataProvider.FindEpisode(new List<SeasonDto>(), 1, 1));
        }

        [Fact]
        public void FindsEpisodesOfARecordedSeries()
        {
            // The pilot of Babylon 5 is episode 0 of season 1.
            var seasons = Fixtures.Read<CursorResponse<SeasonDto>>("seasons_77298.json").Docs;

            var pilot = EpisodeMetadataProvider.FindEpisode(seasons, 1, 0);
            var last = EpisodeMetadataProvider.FindEpisode(seasons, 5, 22);

            Assert.Equal("Встреча", pilot.Name);
            Assert.Equal("Без компромиссов", EpisodeMetadataProvider.FindEpisode(seasons, 5, 1).Name);
            Assert.NotNull(last);
            Assert.Null(EpisodeMetadataProvider.FindEpisode(seasons, 5, 23));
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

        [Fact]
        public void CreateSeasonItemMapsSeasonNumberRussianNameAndSeriesProviderId()
        {
            var result = SeasonMetadataProvider.CreateSeasonItem(new SeasonDto { Number = 2 }, 12345);

            Assert.Equal(2, result.IndexNumber);
            Assert.Equal("Сезон 2", result.Name);
            Assert.Equal("12345", result.ProviderIds[Constants.ProviderId]);
        }

        [Fact]
        public void CreateSeasonItemMapsZeroSeasonAsSpecialsInRussian()
        {
            var result = SeasonMetadataProvider.CreateSeasonItem(new SeasonDto { Number = 0 }, 12345);

            Assert.Equal("Спецвыпуски", result.Name);
        }

        [Fact]
        public void CreateEpisodeItemMapsRussianTitleDescriptionDateAndProviderId()
        {
            var episode = new EpisodeDto
            {
                Number = 3,
                Name = "Название",
                EnName = "Title",
                Description = "Описание",
                AirDate = new System.DateTime(2020, 4, 5, 0, 0, 0, System.DateTimeKind.Utc)
            };

            var result = EpisodeMetadataProvider.CreateEpisodeItem(episode, 2, 12345);

            Assert.Equal(2, result.ParentIndexNumber);
            Assert.Equal(3, result.IndexNumber);
            Assert.Equal("Название", result.Name);
            Assert.Equal("Описание", result.Overview);
            Assert.Equal(new System.DateTime(2020, 4, 5), result.PremiereDate);
            Assert.Equal("12345", result.ProviderIds[Constants.ProviderId]);
        }

        [Fact]
        public void CreateEpisodeItemFallsBackToEnglishTitleWhenRussianTitleIsBlank()
        {
            var episode = new EpisodeDto { Number = 1, Name = " ", EnName = "English title" };

            var result = EpisodeMetadataProvider.CreateEpisodeItem(episode, 1, 12345);

            Assert.Equal("English title", result.Name);
            Assert.Null(result.PremiereDate);
        }
    }
}
