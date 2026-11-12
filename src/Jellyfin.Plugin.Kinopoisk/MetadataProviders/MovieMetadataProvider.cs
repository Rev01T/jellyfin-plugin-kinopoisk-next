using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Kinopoisk.ProviderIdResolvers;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Kinopoisk.MetadataProviders
{
    public class MovieMetadataProvider : BaseVideoMetadataProvider<Movie, MovieInfo>
    {
        public MovieMetadataProvider(IKinopoiskApiClient kinopoiskApiClient, IProviderIdResolver<MovieInfo> providerIdResolver, ILogger<MovieMetadataProvider> logger, IHttpClientFactory httpClientFactory)
            : base(kinopoiskApiClient, providerIdResolver, logger, httpClientFactory)
        {
        }

        protected override Movie ConvertResponseToItem(Film apiResponse)
            => apiResponse.ToMovie();

        protected override Task EnrichItem(Movie item, int kinopoiskId, CancellationToken cancellationToken)
            => ApplyDistributionPremiereDate(item, kinopoiskId, cancellationToken);
    }
}
