using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.Kinopoisk.Tests
{
    public class ApiModelExtensionsTests
    {
        [Fact]
        public void ToPersonInfosShouldSkipPersonsWithoutNamesAndDubbingActors()
        {
            var staff = new List<PersonInMovieDto>
            {
                CreateStaff(1, "Иван Иванов", string.Empty),
                CreateStaff(2, string.Empty, string.Empty),
                CreateStaff(3, "   ", "   "),
                CreateStaff(4, string.Empty, "John Smith"),
                CreateStaff(5, "Дублёр", string.Empty, "voiceover"),
                null
            };

            var result = staff.ToPersonInfos().ToArray();

            Assert.Equal(2, result.Length);
            Assert.Equal("Иван Иванов", result[0].Name);
            Assert.Equal(1, result[0].SortOrder);
            Assert.Equal("John Smith", result[1].Name);
            Assert.Equal(2, result[1].SortOrder);
        }

        [Fact]
        public void ToPersonInfosHandlesMissingList()
        {
            Assert.Empty(((List<PersonInMovieDto>)null).ToPersonInfos());
        }

        [Fact]
        public void ToPersonInfoMapsKindPhotoIdAndRole()
        {
            var person = new PersonInMovieDto
            {
                Id = 3873197,
                Name = "Ирина Старшенбаум",
                Photo = "https://example.test/photo/300x300",
                Description = "Марина",
                Profession = "актеры",
                EnProfession = "actor"
            };

            var result = person.ToPersonInfo();

            Assert.Equal("Ирина Старшенбаум", result.Name);
            Assert.Equal("https://example.test/photo/300x300", result.ImageUrl);
            Assert.Equal(PersonKind.Actor, result.Type);
            Assert.Equal("Марина", result.Role);
            Assert.Equal("3873197", result.ProviderIds[Constants.ProviderId]);
        }

        [Fact]
        public void ToPersonInfoUsesTheProfessionWhenThereIsNoRole()
        {
            var person = new PersonInMovieDto { Id = 1, Name = "Артем Аксененко", Profession = "режиссеры", EnProfession = "director" };

            var result = person.ToPersonInfo();

            Assert.Equal("Режиссеры", result.Role);
            Assert.Equal(PersonKind.Director, result.Type);
        }

        [Theory]
        [InlineData("actor", PersonKind.Actor)]
        [InlineData("director", PersonKind.Director)]
        [InlineData("writer", PersonKind.Writer)]
        [InlineData("composer", PersonKind.Composer)]
        [InlineData("producer", PersonKind.Producer)]
        [InlineData("editor", PersonKind.Editor)]
        [InlineData("translator", PersonKind.Translator)]
        [InlineData("operator", PersonKind.Unknown)]
        [InlineData("design", PersonKind.Unknown)]
        [InlineData("voiceover", PersonKind.Unknown)]
        [InlineData("ACTOR", PersonKind.Actor)]
        [InlineData(null, PersonKind.Unknown)]
        public void ToPersonTypeMapsProfessionKeys(string profession, PersonKind expected)
        {
            Assert.Equal(expected, profession.ToPersonType());
        }

        [Fact]
        public void ToPersonMapsBirthdayDeathDateAndBirthplace()
        {
            var person = new PersonDto
            {
                Name = "Тест",
                Birthday = new DateTime(1980, 2, 3, 0, 0, 0, DateTimeKind.Utc),
                Death = new DateTime(2020, 4, 5, 0, 0, 0, DateTimeKind.Utc),
                BirthPlace = new List<ValueDto> { new ValueDto { Value = "Москва" }, new ValueDto { Value = " " }, new ValueDto { Value = "Россия" } }
            };

            var result = person.ToPerson();

            Assert.Equal(new DateTime(1980, 2, 3), result.PremiereDate);
            Assert.Equal(new DateTime(2020, 4, 5), result.EndDate);
            Assert.Equal(new[] { "Москва, Россия" }, result.ProductionLocations);
        }

        [Fact]
        public void ToPersonFallsBackToTheEnglishName()
        {
            Assert.Equal("John Smith", new PersonDto { Name = " ", EnName = "John Smith" }.ToPerson().Name);
            var withoutPlace = new PersonDto { Name = "Тест" }.ToPerson();
            Assert.True(withoutPlace.ProductionLocations == null || withoutPlace.ProductionLocations.Length == 0);
        }

        [Theory]
        [InlineData(0, "0+")]
        [InlineData(6, "6+")]
        [InlineData(12, "12+")]
        [InlineData(16, "16+")]
        [InlineData(18, "18+")]
        public void NormalizeOfficialRatingMapsKinopoiskAgeLimits(int input, string expected)
        {
            Assert.Equal(expected, ApiModelExtensions.NormalizeOfficialRating(input, null));
        }

        [Fact]
        public void NormalizeOfficialRatingPrefersTheAgeLimitOverMpaa()
        {
            Assert.Equal("12+", ApiModelExtensions.NormalizeOfficialRating(12, "pg13"));
        }

        [Theory]
        [InlineData("pg13", "PG-13")]
        [InlineData("r", "R")]
        [InlineData("nc17", "NC-17")]
        public void NormalizeOfficialRatingNormalizesMpaaValues(string input, string expected)
        {
            Assert.Equal(expected, ApiModelExtensions.NormalizeOfficialRating(null, input));
        }

        [Fact]
        public void NormalizeOfficialRatingIsMissingWithoutValidInput()
        {
            Assert.Null(ApiModelExtensions.NormalizeOfficialRating(null, null));
            Assert.Null(ApiModelExtensions.NormalizeOfficialRating(14, " "));
        }

        [Fact]
        public void RussianCriticsRatingIsAlreadyAPercentageAndIsNotMultiplied()
        {
            // Kinopoisk shows 60%; the API returns 60 (not 6).
            var movie = new MovieDto { Rating = new RatingDto { RussianFilmCritics = 60, FilmCritics = 6.5 } };

            Assert.Equal(60f, movie.GetCriticRating());
        }

        [Fact]
        public void RussianCriticsRatingKeepsFractionalPercentage()
        {
            var movie = new MovieDto { Rating = new RatingDto { RussianFilmCritics = 16.6667 } };

            Assert.Equal(16.6667f, movie.GetCriticRating());
        }

        [Fact]
        public void CriticRatingNeverExceedsOneHundred()
        {
            Assert.Equal(100f, new MovieDto { Rating = new RatingDto { RussianFilmCritics = 100.0 } }.GetCriticRating());
            Assert.Equal(100f, new MovieDto { Rating = new RatingDto { RussianFilmCritics = 670.0 } }.GetCriticRating());
        }

        [Fact]
        public void CriticRatingFallsBackToFilmCriticsTenPointScale()
        {
            Assert.Equal(65f, new MovieDto { Rating = new RatingDto { FilmCritics = 6.5 } }.GetCriticRating());
        }

        [Fact]
        public void FilmCriticsRatingAboveTenIsTreatedAsPercentage()
        {
            Assert.Equal(72f, new MovieDto { Rating = new RatingDto { FilmCritics = 72.0 } }.GetCriticRating());
        }

        [Fact]
        public void CriticRatingIsMissingWithoutCriticRatings()
        {
            Assert.Null(new MovieDto().GetCriticRating());
            Assert.Null(new MovieDto { Rating = new RatingDto { Kp = 7.5 } }.GetCriticRating());
            Assert.Null(((MovieDto)null).GetCriticRating());
        }

        [Fact]
        public void ToMovieMapsRuntimeOverviewFallbackAndGenreCasing()
        {
            var movie = new MovieDto
            {
                MovieLength = 123,
                Description = " ",
                ShortDescription = "Краткое описание",
                Genres = new List<NamedItemDto> { new NamedItemDto { Name = "драма" } }
            };

            var result = movie.ToMovie();

            Assert.Equal(TimeSpan.FromMinutes(123).Ticks, result.RunTimeTicks);
            Assert.Equal("Краткое описание", result.Overview);
            Assert.Contains("Драма", result.Genres);
        }

        [Fact]
        public void ToMovieUsesTheEpisodeLengthForSeries()
        {
            Assert.Equal(TimeSpan.FromMinutes(43).Ticks, new MovieDto { SeriesLength = 43 }.ToSeries().RunTimeTicks);
            Assert.Null(new MovieDto().ToMovie().RunTimeTicks);
        }

        [Fact]
        public void CommunityRatingFallsBackToImdb()
        {
            Assert.Equal(8.01f, new MovieDto { Rating = new RatingDto { Kp = 8.01, Imdb = 7.9 } }.ToMovie().CommunityRating);
            Assert.Equal(7.9f, new MovieDto { Rating = new RatingDto { Kp = 0, Imdb = 7.9 } }.ToMovie().CommunityRating);
            Assert.Null(new MovieDto { Rating = new RatingDto() }.ToMovie().CommunityRating);
        }

        [Fact]
        public void ToSeriesMapsFinishedSeriesStatusAndEndYear()
        {
            var movie = new MovieDto { ReleaseYears = new List<ReleaseYearDto> { new ReleaseYearDto { Start = 2020, End = 2023 } } };

            var result = movie.ToSeries();

            Assert.Equal(SeriesStatus.Ended, result.Status);
            Assert.Equal(new DateTime(2023, 12, 31), result.EndDate);
        }

        [Fact]
        public void ToSeriesMapsRunningSeriesStatus()
        {
            // The end year 0 means that the series is still running.
            var movie = new MovieDto { ReleaseYears = new List<ReleaseYearDto> { new ReleaseYearDto { Start = 2021, End = 0 } } };

            var result = movie.ToSeries();

            Assert.Equal(SeriesStatus.Continuing, result.Status);
            Assert.Null(result.EndDate);
        }

        [Fact]
        public void ToSeriesMapsTheStatusFieldWhenThereAreNoReleaseYears()
        {
            Assert.Equal(SeriesStatus.Ended, new MovieDto { Status = "completed" }.ToSeries().Status);
            Assert.Equal(SeriesStatus.Continuing, new MovieDto().ToSeries().Status);
        }

        [Fact]
        public void LocalAndOriginalNames()
        {
            var foreign = new MovieDto { Name = "Аватар", AlternativeName = "Avatar" };
            var russian = new MovieDto { Name = "Пальма", AlternativeName = "Пальма", Countries = new List<NamedItemDto> { new NamedItemDto { Name = "Россия" } } };
            var onlyOriginal = new MovieDto { AlternativeName = "Avatar" };

            Assert.Equal("Аватар", foreign.GetLocalName());
            Assert.Equal("Avatar", foreign.GetOriginalNameIfNotSame());
            Assert.Equal(string.Empty, russian.GetOriginalNameIfNotSame());
            Assert.Equal("Avatar", onlyOriginal.GetLocalName());
            Assert.Equal(string.Empty, onlyOriginal.GetOriginalNameIfNotSame());
        }

        [Fact]
        public void ToRemoteSearchResultMapsTheSearchHit()
        {
            var movie = new MovieDto
            {
                Id = 301,
                Name = "Матрица",
                Year = 1999,
                Description = "Описание",
                Poster = new ImageDto { Url = "https://example.test/poster/600x900" }
            };

            var result = movie.ToRemoteSearchResult();

            Assert.Equal("Матрица", result.Name);
            Assert.Equal("https://example.test/poster/600x900", result.ImageUrl);
            Assert.Equal(new DateTime(1999, 1, 1), result.PremiereDate);
            Assert.Equal("Описание", result.Overview);
            Assert.Equal("301", result.ProviderIds[Constants.ProviderId]);
            Assert.Null(((MovieDto)null).ToRemoteSearchResult());
        }

        [Fact]
        public void ToRemoteImageInfosOffersThePoster()
        {
            var movie = new MovieDto { Poster = new ImageDto { Url = "https://example.test/poster/600x900" } };

            var image = Assert.Single(movie.ToRemoteImageInfos());

            Assert.Equal(ImageType.Primary, image.Type);
            Assert.Equal("https://example.test/poster/600x900", image.Url);
            Assert.Empty(new MovieDto().ToRemoteImageInfos());
        }

        private static PersonInMovieDto CreateStaff(int id, string name, string enName, string enProfession = "actor")
        {
            return new PersonInMovieDto
            {
                Id = id,
                Name = name,
                EnName = enName,
                Profession = "актеры",
                EnProfession = enProfession
            };
        }
    }
}
