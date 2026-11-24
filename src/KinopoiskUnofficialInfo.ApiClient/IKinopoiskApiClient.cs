using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KinopoiskUnofficialInfo.ApiClient
{
    /// <summary>
    /// Client of the poiskkino.dev API (https://api.poiskkino.dev, version 1.5).
    /// </summary>
    public interface IKinopoiskApiClient
    {
        /// <summary>
        /// Full card of a title: names, ratings, cast and crew, premiere dates, posters.
        /// Returns <c>null</c> when the title does not exist or the ID is not valid.
        /// </summary>
        Task<MovieDto> GetMovie(int movieId, CancellationToken? cancellationToken = null);

        /// <summary>
        /// Titles with the given IMDb ID.
        /// </summary>
        Task<IReadOnlyList<MovieDto>> FindMoviesByImdbId(string imdbId, CancellationToken? cancellationToken = null);

        /// <summary>
        /// Search of titles by name.
        /// </summary>
        Task<SearchResponse<MovieDto>> SearchMovies(string query, int page = 1, CancellationToken? cancellationToken = null);

        /// <summary>
        /// Seasons of a series with their episodes, ordered by the season number.
        /// </summary>
        Task<IReadOnlyList<SeasonDto>> GetSeasons(int movieId, CancellationToken? cancellationToken = null);

        /// <summary>
        /// A person. Returns <c>null</c> when the person does not exist.
        /// </summary>
        Task<PersonDto> GetPerson(int personId, CancellationToken? cancellationToken = null);

        /// <summary>
        /// Usage of the token: limit, used and remaining requests, reset time.
        /// </summary>
        Task<TokenInfoDto> GetTokenInfo(CancellationToken? cancellationToken = null);
    }
}
