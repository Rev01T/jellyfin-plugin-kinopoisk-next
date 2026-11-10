using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Kinopoisk.ProviderIdResolvers;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Kinopoisk.MetadataProviders
{
    public class SeriesMetadataProvider : BaseVideoMetadataProvider<Series, SeriesInfo>
    {
        public SeriesMetadataProvider(IKinopoiskApiClient kinopoiskApiClient, IProviderIdResolver<SeriesInfo> providerIdResolver, ILogger<SeriesMetadataProvider> logger, IHttpClientFactory httpClientFactory)
            : base(kinopoiskApiClient, providerIdResolver, logger, httpClientFactory)
        {
        }

        protected override Series ConvertResponseToItem(Film apiResponse)
            => apiResponse.ToSeries();

        protected override async Task EnrichItem(Series item, int kinopoiskId, CancellationToken cancellationToken)
        {
            var distributions = await _apiClient.GetDistributions(kinopoiskId, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var worldPremiere = distributions?.Items?
                .Where(distribution => distribution != null && distribution.Type == DistributionType.WORLD_PREMIER)
                .Select(distribution => distribution.Date.ParseDate())
                .Where(date => date.HasValue)
                .OrderBy(date => date.Value)
                .FirstOrDefault();

            var premiere = worldPremiere ?? distributions?.Items?
                .Where(distribution => distribution != null && distribution.Type == DistributionType.PREMIERE)
                .Select(distribution => distribution.Date.ParseDate())
                .Where(date => date.HasValue)
                .OrderBy(date => date.Value)
                .FirstOrDefault();

            if (premiere.HasValue)
                item.PremiereDate = premiere.Value;
        }
    }
}
