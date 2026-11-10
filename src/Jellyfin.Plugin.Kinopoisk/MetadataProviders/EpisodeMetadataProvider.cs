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
    /// Resolves episode metadata from the Kinopoisk series seasons endpoint.
    /// </summary>
    public sealed class EpisodeMetadataProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>
    {
        private readonly IKinopoiskApiClient _apiClient;

        public EpisodeMetadataProvider(IKinopoiskApiClient apiClient)
        {
            _apiClient = apiClient ?? throw new System.ArgumentNullException(nameof(apiClient));
        }

        public string Name => Constants.ProviderName;

        public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Episode>
            {
                QueriedById = true,
                Provider = Constants.ProviderName,
                ResultLanguage = Constants.ProviderMetadataLanguage
            };

            if (!SeasonMetadataProvider.TryGetSeriesId(info?.SeriesProviderIds, out var seriesId)
                || !info.ParentIndexNumber.HasValue
                || !info.IndexNumber.HasValue)
                return result;

            var response = await _apiClient.GetSeasons(seriesId, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var episode = response?.Items?
                .Where(season => season != null && season.Number == info.ParentIndexNumber.Value)
                .SelectMany(season => season.Episodes ?? Enumerable.Empty<KinopoiskUnofficialInfo.ApiClient.Episode>())
                .FirstOrDefault(item => item != null
                    && item.SeasonNumber == info.ParentIndexNumber.Value
                    && item.EpisodeNumber == info.IndexNumber.Value);

            if (episode == null)
                return result;

            var item = new Episode
            {
                ParentIndexNumber = episode.SeasonNumber,
                IndexNumber = episode.EpisodeNumber,
                Name = !string.IsNullOrWhiteSpace(episode.NameRu) ? episode.NameRu : episode.NameEn,
                Overview = episode.Synopsis,
                PremiereDate = episode.ReleaseDate.ParseDate()
            };
            item.SetProviderId(Constants.ProviderId, seriesId.ToString(System.Globalization.CultureInfo.InvariantCulture));

            result.Item = item;
            result.HasMetadata = true;
            return result;
        }

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
            => Task.FromResult(Enumerable.Empty<RemoteSearchResult>());
    }
}