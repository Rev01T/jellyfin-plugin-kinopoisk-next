using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using JellyfinSeason = MediaBrowser.Controller.Entities.TV.Season;

namespace Jellyfin.Plugin.Kinopoisk.MetadataProviders
{
    /// <summary>
    /// Resolves season metadata from the Kinopoisk series seasons endpoint.
    /// </summary>
    public sealed class SeasonMetadataProvider : BaseMetadataProvider, IRemoteMetadataProvider<JellyfinSeason, SeasonInfo>
    {
        private readonly IKinopoiskApiClient _apiClient;

        public SeasonMetadataProvider(IKinopoiskApiClient apiClient, IHttpClientFactory httpClientFactory)
            : base(httpClientFactory)
        {
            _apiClient = apiClient ?? throw new System.ArgumentNullException(nameof(apiClient));
        }

        public async Task<MetadataResult<JellyfinSeason>> GetMetadata(SeasonInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<JellyfinSeason>
            {
                QueriedById = true,
                Provider = Constants.ProviderName,
                ResultLanguage = Constants.ProviderMetadataLanguage
            };

            if (info == null || !TryGetSeriesId(info.SeriesProviderIds, out var seriesId) || !info.IndexNumber.HasValue)
                return result;

            var response = await _apiClient.GetSeasons(seriesId, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var season = FindSeason(response, info.IndexNumber.Value);
            if (season == null)
                return result;

            var item = CreateSeasonItem(season, seriesId);

            result.Item = item;
            result.HasMetadata = true;
            return result;
        }

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeasonInfo searchInfo, CancellationToken cancellationToken)
            => Task.FromResult(Enumerable.Empty<RemoteSearchResult>());

        internal static JellyfinSeason CreateSeasonItem(KinopoiskUnofficialInfo.ApiClient.Season season, int seriesId)
        {
            var item = new JellyfinSeason
            {
                IndexNumber = season.Number,
                Name = season.Number == 0 ? "Спецвыпуски" : $"Сезон {season.Number}"
            };
            item.ProviderIds[Constants.ProviderId] = seriesId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return item;
        }

        internal static KinopoiskUnofficialInfo.ApiClient.Season FindSeason(SeasonResponse response, int seasonNumber)
            => response?.Items?.FirstOrDefault(item => item != null && item.Number == seasonNumber);

        internal static bool TryGetSeriesId(IDictionary<string, string> providerIds, out int seriesId)
        {
            seriesId = 0;
            return providerIds != null
                && providerIds.TryGetValue(Constants.ProviderId, out var value)
                && int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out seriesId)
                && seriesId > 0;
        }
    }
}