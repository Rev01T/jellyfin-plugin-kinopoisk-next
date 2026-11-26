using System;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Kinopoisk.Configuration;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.Kinopoisk.Tests
{
    /// <summary>
    /// Maps real responses of the API (recorded on 2026-11-20) to Jellyfin items.
    /// </summary>
    public class RecordedResponseMappingTests
    {
        [Fact]
        public void ToMovieMapsAFilm()
        {
            var movie = Fixtures.Movie(251733).ToMovie();

            Assert.Equal("251733", movie.ProviderIds[Constants.ProviderId]);
            Assert.Equal("tt0499549", movie.ProviderIds[MetadataProvider.Imdb.ToString()]);
            Assert.Equal("Аватар", movie.Name);
            Assert.Equal("Avatar", movie.OriginalTitle);
            Assert.Equal(2009, movie.ProductionYear);
            Assert.Equal(new DateTime(2009, 12, 10), movie.PremiereDate);
            Assert.Equal("Это новый мир", movie.Tagline);
            Assert.Equal("12+", movie.OfficialRating);
            Assert.Equal(TimeSpan.FromMinutes(162).Ticks, movie.RunTimeTicks);
            Assert.Equal(8.0f, movie.CommunityRating);
            Assert.Equal(77f, movie.CriticRating);
            Assert.Equal(new[] { "США", "Великобритания" }, movie.ProductionLocations);
            Assert.Equal(new[] { "Фантастика", "Боевик", "Драма", "Приключения" }, movie.Genres);
            Assert.StartsWith("Бывший морпех", movie.Overview);
        }

        [Fact]
        public void PremiereDateFollowsTheSetting()
        {
            var film = Fixtures.Movie(251733);

            Assert.Equal(new DateTime(2009, 12, 10), film.ToMovie(PremiereDateSource.World).PremiereDate);
            Assert.Equal(new DateTime(2009, 12, 17), film.ToMovie(PremiereDateSource.Russia).PremiereDate);
            // Only the year is stored: no made-up January 1.
            Assert.Null(film.ToMovie(PremiereDateSource.YearOnly).PremiereDate);
            Assert.Equal(2009, film.ToMovie(PremiereDateSource.YearOnly).ProductionYear);
        }

        [Theory]
        [InlineData(1395460, 2021, 4, 16)] // only the digital release is known
        [InlineData(948870, 2015, 11, 29)] // only the Russian release is known
        [InlineData(77298, 1993, 2, 22)]
        [InlineData(89540, 1973, 8, 11)] // a DVD release 36 years later does not matter
        public void PremiereDateIsFoundWhenOnlySomeDatesAreKnown(int id, int year, int month, int day)
        {
            Assert.Equal(new DateTime(year, month, day), Fixtures.Movie(id).ToSeries().PremiereDate);
        }

        [Fact]
        public void ToMovieMapsAFilmWithoutForeignTitleAndWithoutMpaa()
        {
            var movie = Fixtures.Movie(1142206).ToMovie();

            Assert.Equal("Пальма", movie.Name);
            Assert.Equal(string.Empty, movie.OriginalTitle);
            Assert.Equal("6+", movie.OfficialRating);
            // Only Russian critics rated it: 100%.
            Assert.Equal(100f, movie.CriticRating);
        }

        [Fact]
        public void ToSeriesMapsAFinishedSeries()
        {
            var series = Fixtures.Movie(77298).ToSeries();

            Assert.Equal("Вавилон 5", series.Name);
            Assert.Equal("Babylon 5", series.OriginalTitle);
            Assert.Equal(SeriesStatus.Ended, series.Status);
            Assert.Equal(new DateTime(1998, 12, 31), series.EndDate);
            // The length of an episode.
            Assert.Equal(TimeSpan.FromMinutes(43).Ticks, series.RunTimeTicks);
        }

        [Fact]
        public void ToSeriesMapsARunningSeries()
        {
            var series = Fixtures.Movie(4416198).ToSeries();

            Assert.Equal(SeriesStatus.Continuing, series.Status);
            Assert.Null(series.EndDate);
            Assert.Equal("18+", series.OfficialRating);
            // Only Kinopoisk and TMDB rated it.
            Assert.Equal(7.1f, series.CommunityRating);
            Assert.Null(series.CriticRating);
        }

        [Fact]
        public void ToPersonInfosMapsTheCastOfASeries()
        {
            var people = Fixtures.Movie(1395460).Persons.ToPersonInfos().ToArray();

            // 31 people in the response, one of them is a dubbing actor.
            Assert.Equal(30, people.Length);
            Assert.Equal("Андрей Бурковский", people[0].Name);
            Assert.Equal(PersonKind.Actor, people[0].Type);
            Assert.Equal("1769503", people[0].ProviderIds[Constants.ProviderId]);
            Assert.Equal(1, people[0].SortOrder);
            Assert.Equal(30, people[^1].SortOrder);
            Assert.Equal(10, people.Count(person => person.Type == PersonKind.Actor));
            Assert.Equal("Артем Аксененко", Assert.Single(people.Where(person => person.Type == PersonKind.Director)).Name);
            Assert.Equal(4, people.Count(person => person.Type == PersonKind.Producer));
            Assert.Equal(4, people.Count(person => person.Type == PersonKind.Writer));
            Assert.Equal(4, people.Count(person => person.Type == PersonKind.Composer));
            Assert.Equal(3, people.Count(person => person.Type == PersonKind.Editor));
            // Cameramen and artists have no Jellyfin kind.
            Assert.Equal(4, people.Count(person => person.Type == PersonKind.Unknown && person.Role != null));
        }

        [Fact]
        public void ToPersonInfosDropsTheDubbingActorsOfAFilm()
        {
            var movie = Fixtures.Movie(251733);

            var people = movie.Persons.ToPersonInfos().ToArray();

            Assert.Equal(88, movie.Persons.Count);
            Assert.Equal(74, people.Length);
            Assert.Equal("Сэм Уортингтон", people[0].Name);
            Assert.Equal("Джеймс Кэмерон", Assert.Single(people.Where(person => person.Type == PersonKind.Director)).Name);
        }

        [Fact]
        public void ToPersonMapsARecordedPerson()
        {
            var dto = Fixtures.Read<PersonDto>("person_3873197.json");

            var person = dto.ToPerson();
            var image = dto.ToRemoteImageInfo();

            Assert.Equal("Ирина Старшенбаум", person.Name);
            Assert.Equal(new DateTime(1992, 3, 30), person.PremiereDate);
            Assert.Null(person.EndDate);
            Assert.Equal(new[] { "Москва, Россия" }, person.ProductionLocations);
            Assert.Equal(ImageType.Primary, image.Type);
            Assert.EndsWith("/orig", image.Url);
        }

        [Fact]
        public void ToRemoteSearchResultsMapsARecordedSearch()
        {
            var results = Fixtures.Read<SearchResponse<MovieDto>>("search_matrix.json")
                .ToRemoteSearchResults(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance)
                .ToArray();

            Assert.Equal(10, results.Length);
            Assert.Equal("Матрица", results[0].Name);
            Assert.Equal("301", results[0].ProviderIds[Constants.ProviderId]);
            Assert.Equal(new DateTime(1999, 1, 1), results[0].PremiereDate);
            Assert.StartsWith("https://", results[0].ImageUrl);
            // The one without a poster has no image URL.
            Assert.Null(results.Single(result => result.ProviderIds[Constants.ProviderId] == "1227434").ImageUrl);
            Assert.All(results, result => Assert.Equal(Constants.ProviderName, result.SearchProviderName));
        }

        [Fact]
        public void ToRemoteImageInfosOffersThePosterOfARecordedTitle()
        {
            var image = Assert.Single(Fixtures.Movie(251733).ToRemoteImageInfos());

            Assert.EndsWith("/600x900", image.Url);
            Assert.Equal(Constants.ProviderMetadataLanguage, image.Language);
        }
    }
}
