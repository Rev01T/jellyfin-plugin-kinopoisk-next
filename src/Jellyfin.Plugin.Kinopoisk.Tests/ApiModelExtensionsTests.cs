using System.Collections.Generic;
using System.Linq;
using KinopoiskUnofficialInfo.ApiClient;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.Kinopoisk.Tests
{
    public class ApiModelExtensionsTests
    {
        [Fact]
        public void ToPersonInfosShouldSkipPersonsWithoutNames()
        {
            var staff = new List<StaffResponse>
            {
                CreateStaff(1, "Иван Иванов", string.Empty),
                CreateStaff(2, string.Empty, string.Empty),
                CreateStaff(3, "   ", "   "),
                CreateStaff(4, string.Empty, "John Smith")
            };

            var result = staff.ToPersonInfos().ToArray();

            Assert.Equal(2, result.Length);
            Assert.Equal("Иван Иванов", result[0].Name);
            Assert.Equal(1, result[0].SortOrder);
            Assert.Equal("John Smith", result[1].Name);
            Assert.Equal(2, result[1].SortOrder);
        }

        [Fact]
        public void ToRemoteImageInfosUsesOriginalImageUrlInsteadOfPreviewUrl()
        {
            var response = new FilmImagesResponse
            {
                Items = new List<FilmImage>
                {
                    new FilmImage
                    {
                        ImageUrl = "https://images.example/original",
                        PreviewUrl = "https://images.example/300x"
                    },
                    new FilmImage
                    {
                        ImageUrl = null,
                        PreviewUrl = "https://images.example/preview-only"
                    }
                }
            };

            var result = response.ToRemoteImageInfos(MediaBrowser.Model.Entities.ImageType.Primary).ToArray();

            Assert.Single(result);
            Assert.Equal("https://images.example/original", result[0].Url);
            Assert.Equal(MediaBrowser.Model.Entities.ImageType.Primary, result[0].Type);
        }

        [Theory]
        [InlineData("1992-03-30", 1992, 3, 30)]
        [InlineData("1992-03-30T00:00:00.0000000Z", 1992, 3, 30)]
        public void ParseDateSupportsDateOnlyAndRoundTripValues(string value, int year, int month, int day)
        {
            var result = value.ParseDate();

            Assert.Equal(new System.DateTime(year, month, day), result);
        }

        [Fact]
        public void ToPersonMapsBirthdayAndDeathDate()
        {
            var person = new PersonResponse
            {
                NameRu = "Тест",
                Birthday = "1980-02-03",
                Death = "2020-04-05"
            };

            var result = person.ToPerson();

            Assert.Equal(new System.DateTime(1980, 2, 3), result.PremiereDate);
            Assert.Equal(new System.DateTime(2020, 4, 5), result.EndDate);
        }

        [Theory]
        [InlineData("age6", "6+")]
        [InlineData("age12", "12+")]
        [InlineData("age16", "16+")]
        [InlineData("age18", "18+")]
        [InlineData("age0", "0+")]
        public void NormalizeOfficialRatingMapsKinopoiskAgeLimits(string input, string expected)
        {
            Assert.Equal(expected, ApiModelExtensions.NormalizeOfficialRating(input, null));
        }

        [Theory]
        [InlineData("pg13", "PG-13")]
        [InlineData("r", "R")]
        public void NormalizeOfficialRatingNormalizesMpaaValues(string input, string expected)
        {
            Assert.Equal(expected, ApiModelExtensions.NormalizeOfficialRating(null, input));
        }

        [Fact]
        public void CriticRatingIsConvertedToZeroToOneHundredScale()
        {
            var film = new Film { RatingRfCritics = 7.8, RatingFilmCritics = 6.5 };

            Assert.Equal(78f, film.GetCriticRatingAsTenPointBased());
        }

        [Fact]
        public void CriticRatingFallsBackToFilmCriticsWhenRfCriticsMissing()
        {
            var film = new Film { RatingFilmCritics = 6.5 };

            Assert.Equal(65f, film.GetCriticRatingAsTenPointBased());
        }

        [Fact]
        public void ToMovieMapsRuntimeOverviewFallbackAndGenreCasing()
        {
            var film = new Film
            {
                FilmLength = 123,
                Description = " ",
                ShortDescription = "Краткое описание",
                Genres = new List<Genre> { new Genre { Genre1 = "драма" } }
            };

            var result = film.ToMovie();

            Assert.Equal(System.TimeSpan.FromMinutes(123).Ticks, result.RunTimeTicks);
            Assert.Equal("Краткое описание", result.Overview);
            Assert.Contains("Драма", result.Genres);
        }

        [Fact]
        public void ToSeriesMapsCompletedStatusAndEndYear()
        {
            var film = new Film { Completed = true, EndYear = 2023 };

            var result = film.ToSeries();

            Assert.Equal(SeriesStatus.Ended, result.Status);
            Assert.Equal(new System.DateTime(2023, 12, 31), result.EndDate);
        }

        [Fact]
        public void ToSeriesMapsUncompletedStatus()
        {
            var film = new Film { Completed = false };

            var result = film.ToSeries();

            Assert.Equal(SeriesStatus.Continuing, result.Status);
            Assert.Null(result.EndDate);
        }

        [Fact]
        public void VoiceDirectorAndOperatorAreNotMappedAsFilmDirector()
        {
            Assert.Equal(PersonKind.Unknown, StaffResponseProfessionKey.VOICE_DIRECTOR.ToPersonType());
            Assert.Equal(PersonKind.Unknown, StaffResponseProfessionKey.OPERATOR.ToPersonType());
            Assert.Equal(PersonKind.Director, StaffResponseProfessionKey.DIRECTOR.ToPersonType());
        }

        private static StaffResponse CreateStaff(
            int id,
            string nameRu,
            string nameEn)
        {
            return new StaffResponse
            {
                StaffId = id,
                NameRu = nameRu,
                NameEn = nameEn,
                PosterUrl = string.Empty,
                ProfessionText = "Актёр",
                ProfessionKey = StaffResponseProfessionKey.ACTOR
            };
        }
    }
}
