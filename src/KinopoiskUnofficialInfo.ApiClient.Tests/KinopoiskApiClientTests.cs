using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KinopoiskUnofficialInfo.ApiClient.Tests
{
    /// <summary>
    /// Runs the client against real recorded API responses (the <c>fixtures</c> folder).
    /// </summary>
    public class KinopoiskApiClientTests
    {
        private static readonly Regex MovieIdPath = new Regex(@"^/v1\.5/movie/(\d+)$");

        private static (KinopoiskApiClient Client, RoutingHandler Handler, ListLogger<KinopoiskApiClient> Logger) Create(Func<string, FakeResponse> route = null)
        {
            var handler = new RoutingHandler(route ?? RecordedApi);
            var logger = new ListLogger<KinopoiskApiClient>();
            var client = new KinopoiskApiClient("test-token", logger, new SingleHandlerClientFactory(handler));
            return (client, handler, logger);
        }

        /// <summary>The recorded responses, keyed by the request like the real API would answer it.</summary>
        private static FakeResponse RecordedApi(string target)
        {
            var movie = MovieIdPath.Match(target);
            if (movie.Success)
            {
                var id = movie.Groups[1].Value;
                return System.IO.File.Exists(System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", $"movie_{id}.json"))
                    ? FakeResponse.Fixture($"movie_{id}.json")
                    : FakeResponse.Error(400, Fixtures.Read("movie_invalid_id.json"));
            }

            if (target.StartsWith("/v1.5/movie/search?", StringComparison.Ordinal))
                return FakeResponse.Fixture("search_matrix.json");
            if (target.StartsWith("/v1.5/movie?externalId.imdb=", StringComparison.Ordinal))
                return FakeResponse.Fixture("imdb_matrix.json");
            if (target.StartsWith("/v1.5/season?movieId=77298", StringComparison.Ordinal))
                return FakeResponse.Fixture("seasons_77298.json");
            if (target.StartsWith("/v1.5/season?movieId=4416198", StringComparison.Ordinal))
                return FakeResponse.Fixture("seasons_4416198.json");
            if (target == "/v1.5/person/3873197")
                return FakeResponse.Fixture("person_3873197.json");
            if (target == "/v1.5/token")
                return FakeResponse.Fixture("token.json");

            return FakeResponse.Error(404, "{\"message\":\"Not Found\",\"statusCode\":404}");
        }

        [Theory]
        [InlineData(1142206, "Пальма", "Пальма", "movie", 2020)]
        [InlineData(1044982, "Шпион, который меня кинул", "The Spy Who Dumped Me", "movie", 2018)]
        [InlineData(1445243, "Будь моим Кириллом", "Будь моим Кириллом", "movie", 2021)]
        [InlineData(251733, "Аватар", "Avatar", "movie", 2009)]
        [InlineData(4416198, "В активном поиске", "В активном поиске", "tv-series", 2021)]
        [InlineData(77298, "Вавилон 5", "Babylon 5", "tv-series", 1993)]
        [InlineData(89540, "Семнадцать мгновений весны", "Семнадцать мгновений весны", "tv-series", 1973)]
        [InlineData(948870, "Где логика?", "Где логика?", "tv-series", 2015)]
        [InlineData(1395460, "Медиатор", "Медиатор", "tv-series", 2021)]
        public async Task GetMovie_ParsesTheCard(int movieId, string name, string alternativeName, string type, int year)
        {
            var (client, handler, logger) = Create();

            var movie = await client.GetMovie(movieId);

            Assert.NotNull(movie);
            Assert.Equal(movieId, movie.Id);
            Assert.Equal(name, movie.Name);
            Assert.Equal(alternativeName, movie.AlternativeName);
            Assert.Equal(type, movie.Type);
            Assert.Equal(year, movie.Year);
            Assert.False(string.IsNullOrWhiteSpace(movie.Description));
            Assert.False(string.IsNullOrWhiteSpace(movie.Poster?.Url));
            Assert.NotEmpty(movie.Genres);
            Assert.NotEmpty(movie.Countries);
            Assert.NotEmpty(movie.Persons);
            Assert.False(string.IsNullOrWhiteSpace(movie.ExternalId?.Imdb));
            // Every field of the recorded response was readable.
            Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
            Assert.Equal(new[] { $"/v1.5/movie/{movieId}" }, handler.Requests);
        }

        [Fact]
        public async Task GetMovie_ParsesDetailsOfAFilm()
        {
            var (client, _, _) = Create();

            var movie = await client.GetMovie(251733);

            Assert.Equal("tt0499549", movie.ExternalId.Imdb);
            Assert.Equal(19995, movie.ExternalId.Tmdb);
            Assert.Equal(162, movie.MovieLength);
            Assert.Equal(12, movie.AgeRating);
            Assert.Equal("pg13", movie.RatingMpaa);
            Assert.Equal("Это новый мир", movie.Slogan);
            Assert.Equal(new[] { "фантастика", "боевик", "драма", "приключения" }, movie.Genres.Select(genre => genre.Name).ToArray());
            Assert.Equal(new[] { "США", "Великобритания" }, movie.Countries.Select(country => country.Name).ToArray());
            Assert.Equal(8.01, movie.Rating.Kp);
            Assert.Equal(7.9, movie.Rating.Imdb);
            Assert.Equal(7.8, movie.Rating.FilmCritics);
            Assert.Equal(76.9231, movie.Rating.RussianFilmCritics);
            Assert.EndsWith("/600x900", movie.Poster.Url);
        }

        [Fact]
        public async Task GetMovie_ParsesPremiereDatesAsUtcDates()
        {
            var (client, _, _) = Create();

            var film = await client.GetMovie(251733);
            var series = await client.GetMovie(1395460);

            Assert.Equal(new DateTime(2009, 12, 10, 0, 0, 0, DateTimeKind.Utc), film.Premiere.World);
            Assert.Equal(new DateTime(2009, 12, 17, 0, 0, 0, DateTimeKind.Utc), film.Premiere.Russia);
            Assert.Equal(new DateTime(2010, 8, 26, 0, 0, 0, DateTimeKind.Utc), film.Premiere.Rerelease);
            // A series with a single known date: the others are absent.
            Assert.Equal(new DateTime(2021, 4, 16, 0, 0, 0, DateTimeKind.Utc), series.Premiere.Digital);
            Assert.Null(series.Premiere.World);
            Assert.Null(series.Premiere.Russia);
        }

        [Fact]
        public async Task GetMovie_ParsesTheCast()
        {
            var (client, _, _) = Create();

            var movie = await client.GetMovie(1395460);

            Assert.Equal(31, movie.Persons.Count);
            var first = movie.Persons.First();
            Assert.Equal(1769503, first.Id);
            Assert.Equal("Андрей Бурковский", first.Name);
            Assert.Equal("actor", first.EnProfession);
            Assert.Equal("актеры", first.Profession);
            Assert.Equal(1, first.ProfessionId);
            Assert.StartsWith("https://", first.Photo);
            Assert.Contains(movie.Persons, person => person.EnProfession == "director" && person.Name == "Артем Аксененко");
            // Not everybody has a photo.
            Assert.Contains(movie.Persons, person => string.IsNullOrEmpty(person.Photo));
        }

        [Theory]
        [InlineData(77298, 1998)]
        [InlineData(1395460, 2024)]
        [InlineData(4416198, 0)]
        public async Task GetMovie_ParsesReleaseYearsOfSeries(int movieId, int endYear)
        {
            var (client, _, _) = Create();

            var movie = await client.GetMovie(movieId);

            Assert.True(movie.IsSeries);
            Assert.Equal(endYear, movie.ReleaseYears.Last().End);
        }

        [Fact]
        public async Task GetMovie_InvalidId_ReturnsNull()
        {
            // The API answers HTTP 400 for an ID outside of 250..99999999.
            var (client, _, _) = Create();

            Assert.Null(await client.GetMovie(1));
        }

        [Fact]
        public async Task GetMovie_NotFound_ReturnsNull()
        {
            var (client, _, _) = Create(_ => FakeResponse.Error(404, "{\"message\":\"Not Found\"}"));

            Assert.Null(await client.GetMovie(99999990));
        }

        [Fact]
        public async Task GetMovie_ServerError_Throws()
        {
            var (client, _, _) = Create(_ => FakeResponse.Error(500, "{\"message\":\"oops\"}"));

            var exception = await Assert.ThrowsAsync<ApiException>(() => client.GetMovie(301));

            Assert.Equal(500, exception.StatusCode);
            Assert.False(exception.IsQuotaExceeded);
        }

        [Fact]
        public async Task GetMovie_SkipsAFieldItCannotReadAndKeepsTheRest()
        {
            var body = "{\"id\":301,\"name\":\"Матрица\",\"year\":\"unknown\",\"movieLength\":136,\"premiere\":{\"world\":\"not a date\",\"russia\":\"2000-03-31T00:00:00.000Z\"}}";
            var (client, _, logger) = Create(_ => FakeResponse.Ok(body));

            var movie = await client.GetMovie(301);

            Assert.Equal("Матрица", movie.Name);
            Assert.Null(movie.Year);
            Assert.Equal(136, movie.MovieLength);
            Assert.Null(movie.Premiere.World);
            Assert.Equal(new DateTime(2000, 3, 31, 0, 0, 0, DateTimeKind.Utc), movie.Premiere.Russia);
            Assert.Equal(2, logger.Entries.Count(entry => entry.Level == LogLevel.Warning));
        }

        [Fact]
        public async Task SearchMovies_ParsesTheResults()
        {
            var (client, handler, _) = Create();

            var response = await client.SearchMovies("Матрица");

            Assert.Equal(315, response.Total);
            Assert.Equal(32, response.Pages);
            Assert.Equal(1, response.Page);
            Assert.Equal(10, response.Docs.Count);
            var first = response.Docs.First();
            Assert.Equal(301, first.Id);
            Assert.Equal("Матрица", first.Name);
            Assert.Equal("The Matrix", first.AlternativeName);
            Assert.Equal(1999, first.Year);
            Assert.Equal("movie", first.Type);
            Assert.False(string.IsNullOrWhiteSpace(first.Poster?.Url));
            // A series with the same name is among the results and is told apart by the year.
            Assert.Contains(response.Docs, movie => movie.Id == 400787 && movie.Year == 1993 && movie.Type == "tv-series");
            Assert.Equal(new[] { "/v1.5/movie/search?query=Матрица&page=1&limit=10" }, handler.Requests);
        }

        [Fact]
        public async Task SearchMovies_EscapesTheQuery()
        {
            var (client, handler, _) = Create();

            await client.SearchMovies("Fast & Furious=1", 3);

            Assert.Equal(new[] { "/v1.5/movie/search?query=Fast%20%26%20Furious%3D1&page=3&limit=10" }, handler.RawRequests);
        }

        [Fact]
        public async Task FindMoviesByImdbId_ReturnsTheTitle()
        {
            var (client, handler, _) = Create();

            var movies = await client.FindMoviesByImdbId("tt0133093");

            Assert.Equal(301, Assert.Single(movies).Id);
            Assert.Equal(new[] { "/v1.5/movie?externalId.imdb=tt0133093&limit=2" }, handler.Requests);
        }

        [Fact]
        public async Task GetSeasons_ParsesAndOrdersSeasons()
        {
            var (client, handler, _) = Create();

            var seasons = await client.GetSeasons(77298);

            // The API returns the seasons in descending order.
            Assert.Equal(new int?[] { 1, 2, 3, 4, 5 }, seasons.Select(season => season.Number).ToArray());
            Assert.Equal(new int?[] { 23, 22, 22, 22, 22 }, seasons.Select(season => season.EpisodesCount).ToArray());
            Assert.All(seasons, season => Assert.Equal(season.EpisodesCount, season.Episodes.Count));
            var pilot = seasons.First().Episodes.First();
            Assert.Equal(0, pilot.Number);
            Assert.Equal("Встреча", pilot.Name);
            Assert.Equal("The Gathering", pilot.EnName);
            Assert.Equal(new DateTime(1997, 8, 17, 0, 0, 0, DateTimeKind.Utc), pilot.AirDate);
            Assert.Equal(new[] { "/v1.5/season?movieId=77298&limit=250" }, handler.Requests);
        }

        [Fact]
        public async Task GetSeasons_ParsesEpisodeDescriptions()
        {
            var (client, _, _) = Create();

            var seasons = await client.GetSeasons(4416198);

            Assert.Equal(2, seasons.Count);
            var episode = seasons.First().Episodes.First();
            Assert.Equal(1, episode.Number);
            Assert.StartsWith("К бизнесмену Андрею", episode.Description);
            Assert.Equal(new DateTime(2021, 4, 8, 0, 0, 0, DateTimeKind.Utc), episode.AirDate);
        }

        [Fact]
        public async Task GetSeasons_FollowsTheCursor()
        {
            var (client, handler, _) = Create(target => target.Contains("&next=")
                ? FakeResponse.Ok("{\"docs\":[{\"number\":1,\"episodes\":[]}],\"hasNext\":false,\"next\":null}")
                : FakeResponse.Ok("{\"docs\":[{\"number\":2,\"episodes\":[]}],\"hasNext\":true,\"next\":\"a b+c\"}"));

            var seasons = await client.GetSeasons(77298);

            Assert.Equal(new int?[] { 1, 2 }, seasons.Select(season => season.Number).ToArray());
            Assert.Equal(new[]
            {
                "/v1.5/season?movieId=77298&limit=250",
                "/v1.5/season?movieId=77298&limit=250&next=a b+c"
            }, handler.Requests);
        }

        [Fact]
        public async Task GetSeasons_ReturnsNothingForATitleWithoutSeasons()
        {
            var (client, _, _) = Create(_ => FakeResponse.Ok("{\"docs\":[],\"limit\":250,\"next\":null,\"prev\":null,\"hasNext\":false,\"hasPrev\":false}"));

            Assert.Empty(await client.GetSeasons(301));
        }

        [Fact]
        public async Task GetPerson_ParsesThePerson()
        {
            var (client, _, _) = Create();

            var person = await client.GetPerson(3873197);

            Assert.Equal(3873197, person.Id);
            Assert.Equal("Ирина Старшенбаум", person.Name);
            Assert.Null(person.EnName);
            Assert.Equal(new DateTime(1992, 3, 30, 0, 0, 0, DateTimeKind.Utc), person.Birthday);
            Assert.Null(person.Death);
            Assert.Equal(new[] { "Москва", "Россия" }, person.BirthPlace.Select(place => place.Value).ToArray());
            Assert.EndsWith("/orig", person.Photo);
        }

        [Fact]
        public async Task GetPerson_NotFound_ReturnsNull()
        {
            var (client, _, _) = Create();

            Assert.Null(await client.GetPerson(5));
        }

        [Fact]
        public async Task GetTokenInfo_ParsesTheUsage()
        {
            var (client, _, _) = Create();

            var info = await client.GetTokenInfo();

            Assert.Equal(200, info.RequestsLimit);
            Assert.Equal(14, info.RequestsUsed);
            Assert.Equal(186, info.RequestsRemaining);
            Assert.Equal(42779, info.Ttl);
            Assert.Equal(new DateTimeOffset(2026, 11, 20, 20, 59, 59, 723, TimeSpan.Zero), KinopoiskQuota.ToResetTime(info.ResetAt));
        }

        [Fact]
        public async Task GetTrailers_AsksForTheVideosOnly()
        {
            var body = "{\"docs\":[{\"id\":301,\"videos\":{\"trailers\":[" +
                "{\"url\":\"https://www.youtube.com/embed/abc\",\"name\":\"Official Trailer\",\"site\":\"youtube\",\"type\":\"TRAILER\"}," +
                "{\"url\":\"https://example.test/video.mp4\",\"name\":\"Other\",\"site\":\"other\",\"type\":\"TEASER\"}]}}],\"hasNext\":false}";
            var (client, handler, _) = Create(_ => FakeResponse.Ok(body));

            var trailers = await client.GetTrailers(301);

            Assert.Equal(2, trailers.Count);
            Assert.Equal("https://www.youtube.com/embed/abc", trailers[0].Url);
            Assert.Equal("youtube", trailers[0].Site);
            Assert.Equal(new[] { "/v1.5/movie?id=301&selectFields=id&selectFields=videos&limit=1" }, handler.Requests);
        }

        [Fact]
        public async Task GetTrailers_ReturnsNothingWhenThereAreNone()
        {
            var (client, _, _) = Create(_ => FakeResponse.Ok("{\"docs\":[{\"id\":301}],\"hasNext\":false}"));

            Assert.Empty(await client.GetTrailers(301));
        }

        [Fact]
        public async Task SendsRequestsToTheConfiguredBaseUrl()
        {
            Uri requested = null;
            var handler = new RoutingHandler(_ => FakeResponse.Fixture("token.json"));
            var client = new KinopoiskApiClient(
                "test-token",
                NullLogger<KinopoiskApiClient>.Instance,
                new SingleHandlerClientFactory(new CapturingHandler(handler, uri => requested = uri)),
                baseUrl: "https://example.test/api/");

            await client.GetTokenInfo();

            Assert.Equal("https://example.test/api/v1.5/token", requested.ToString());
        }

        [Fact]
        public async Task DefaultBaseUrlIsThePoiskkinoApi()
        {
            Uri requested = null;
            var handler = new RoutingHandler(_ => FakeResponse.Fixture("token.json"));
            var client = new KinopoiskApiClient(
                "test-token",
                NullLogger<KinopoiskApiClient>.Instance,
                new SingleHandlerClientFactory(new CapturingHandler(handler, uri => requested = uri)));

            await client.GetTokenInfo();

            Assert.Equal("https://api.poiskkino.dev/v1.5/token", requested.ToString());
        }

        private sealed class CapturingHandler : System.Net.Http.DelegatingHandler
        {
            private readonly Action<Uri> _capture;

            public CapturingHandler(System.Net.Http.HttpMessageHandler inner, Action<Uri> capture)
                : base(inner)
            {
                _capture = capture;
            }

            protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            {
                _capture(request.RequestUri);
                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}
