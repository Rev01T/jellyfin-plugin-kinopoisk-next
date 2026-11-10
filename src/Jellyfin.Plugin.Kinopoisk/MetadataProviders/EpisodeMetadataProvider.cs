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
using JellyfinEpisode = MediaBrowser.Controller.Entities.TV.Episode;

namespace Jellyfin.Plugin.Kinopoisk.MetadataProviders
{
    /// <summary>
    /// Resolves episode metadata from the Kinopoisk series seasons endpoint.
    /// </summary>
    public sealed class EpisodeMetadataProvider : BaseMetadataProvider, IRemoteMetadataProvider<JellyfinEpisode, EpisodeInfo>
    {
        private readonly IKinopoiskApiClient _apiClient;

        public EpisodeMetadataProvider(IKinopoiskApiClient apiClient, IHttpClientFactory httpClientFactory)
            : base(httpClientFactory)
        {
            _apiClient = apiClient ?? throw new System.ArgumentNullException(nameof(apiClient));
        }

        public async Task<MetadataResult<JellyfinEpisode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<JellyfinEpisode>
            {
                QueriedById = true,
                Provider = Constants.ProviderName,
                ResultLanguage = Constants.ProviderMetadataLanguage
            };

            if (info == null
                || !SeasonMetadataProvider.TryGetSeriesId(info.SeriesProviderIds, out var seriesId)
                || !info.ParentIndexNumber.HasValue
                || !info.IndexNumber.HasValue)
                return result;

            var response = await _apiClient.GetSeasons(seriesId, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var episode = FindEpisode(response, info.ParentIndexNumber.Value, info.IndexNumber.Value);

            if (episode == null)
                return result;

            var item = new JellyfinEpisode
            {
                ParentIndexNumber = episode.SeasonNumber,
                IndexNumber = episode.EpisodeNumber,
                Name = !string.IsNullOrWhiteSpace(episode.NameRu) ? episode.NameRu : episode.NameEn,
                Overview = episode.Synopsis,
                PremiereDate = episode.ReleaseDate.ParseDate()
            };
            item.ProviderIds[Constants.ProviderId] = seriesId.ToString(System.Globalization.CultureInfo.InvariantCulture);

            result.Item = item;
            result.HasMetadata = true;
            return result;
        }

        internal static KinopoiskUnofficialInfo.ApiClient.Episode FindEpisode(SeasonResponse response, int seasonNumber, int episodeNumber)
            => response?.Items?
                .Where(season => season != null && season.Number == seasonNumber)
                .SelectMany(season => season.Episodes ?? Enumerable.Empty<KinopoiskUnofficialInfo.ApiClient.Episode>())
                .FirstOrDefault(item => item != null
                    && item.SeasonNumber == seasonNumber
                    && item.EpisodeNumber == episodeNumber);

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
            => Task.FromResult(Enumerable.Empty<RemoteSearchResult>());
    }
}