using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Kinopoisk.MetadataProviders
{
    /// <summary>
    /// Resolves season metadata from the Kinopoisk series seasons endpoint.
    /// </summary>
    public sealed class SeasonMetadataProvider : IRemoteMetadataProvider<Season, SeasonInfo>
    {
        private readonly IKinopoiskApiClient _apiClient;

        public SeasonMetadataProvider(IKinopoiskApiClient apiClient)
        {
            _apiClient = apiClient ?? throw new System.ArgumentNullException(nameof(apiClient));
        }

        public string Name => Constants.ProviderName;

        public async Task<MetadataResult<Season>> GetMetadata(SeasonInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Season>
            {
                QueriedById = true,
                Provider = Constants.ProviderName,
                ResultLanguage = Constants.ProviderMetadataLanguage
            };

            if (!TryGetSeriesId(info?.SeriesProviderIds, out var seriesId) || !info.IndexNumber.HasValue)
                return result;

            var response = await _apiClient.GetSeasons(seriesId, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var season = response?.Items?.FirstOrDefault(item => item != null && item.Number == info.IndexNumber.Value);
            if (season == null)
                return result;

            var item = new Season
            {
                IndexNumber = season.Number,
                Name = season.Number == 0 ? "Specials" : $"Season {season.Number}"
            };
            item.SetProviderId(Constants.ProviderId, seriesId.ToString(System.Globalization.CultureInfo.InvariantCulture));

            result.Item = item;
            result.HasMetadata = true;
            return result;
        }

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeasonInfo searchInfo, CancellationToken cancellationToken)
            => Task.FromResult(Enumerable.Empty<RemoteSearchResult>());

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