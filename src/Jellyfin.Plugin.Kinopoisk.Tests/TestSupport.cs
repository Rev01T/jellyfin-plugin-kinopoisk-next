using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using KinopoiskUnofficialInfo.ApiClient;
using Newtonsoft.Json;

namespace Jellyfin.Plugin.Kinopoisk.Tests
{
    /// <summary>Recorded responses of the API (the fixtures are shared with the API client tests).</summary>
    internal static class Fixtures
    {
        public static T Read<T>(string name) where T : class
            => JsonConvert.DeserializeObject<T>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", name)));

        public static MovieDto Movie(int id) => Read<MovieDto>($"movie_{id}.json");
    }

    /// <summary>An API client with canned answers that counts the requests.</summary>
    internal sealed class FakeApiClient : IKinopoiskApiClient
    {
        public Func<int, MovieDto> Movie { get; set; } = _ => null;
        public Func<int, IReadOnlyList<SeasonDto>> Seasons { get; set; } = _ => new List<SeasonDto>();
        public Func<int, PersonDto> Person { get; set; } = _ => null;
        public Exception Failure { get; set; }

        public int MovieRequests { get; private set; }
        public int SeasonRequests { get; private set; }
        public int PersonRequests { get; private set; }
        public int TotalRequests => MovieRequests + SeasonRequests + PersonRequests;

        public Task<MovieDto> GetMovie(int movieId, CancellationToken? cancellationToken = null)
        {
            MovieRequests++;
            return Respond(() => Movie(movieId));
        }

        public Task<IReadOnlyList<SeasonDto>> GetSeasons(int movieId, CancellationToken? cancellationToken = null)
        {
            SeasonRequests++;
            return Respond(() => Seasons(movieId));
        }

        public Task<PersonDto> GetPerson(int personId, CancellationToken? cancellationToken = null)
        {
            PersonRequests++;
            return Respond(() => Person(personId));
        }

        public Task<IReadOnlyList<MovieDto>> FindMoviesByImdbId(string imdbId, CancellationToken? cancellationToken = null)
            => throw new NotImplementedException();

        public Task<SearchResponse<MovieDto>> SearchMovies(string query, int page = 1, CancellationToken? cancellationToken = null)
            => throw new NotImplementedException();

        public Task<TokenInfoDto> GetTokenInfo(CancellationToken? cancellationToken = null)
            => throw new NotImplementedException();

        private Task<T> Respond<T>(Func<T> answer)
            => Failure != null ? Task.FromException<T>(Failure) : Task.FromResult(answer());
    }

    /// <summary>Every image URL is "found".</summary>
    internal sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient(new OkHandler());

        private sealed class OkHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
