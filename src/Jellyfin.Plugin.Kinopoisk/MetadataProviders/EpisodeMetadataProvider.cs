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
using Microsoft.Extensions.Logging;
using JellyfinEpisode = MediaBrowser.Controller.Entities.TV.Episode;

namespace Jellyfin.Plugin.Kinopoisk.MetadataProviders
{
    /// <summary>
    /// Resolves episode metadata from the Kinopoisk seasons of a series.
    /// </summary>
    public sealed class EpisodeMetadataProvider : BaseMetadataProvider, IRemoteMetadataProvider<JellyfinEpisode, EpisodeInfo>
    {
        private readonly IKinopoiskApiClient _apiClient;
        private readonly ILogger<EpisodeMetadataProvider> _logger;

        public EpisodeMetadataProvider(IKinopoiskApiClient apiClient, IHttpClientFactory httpClientFactory, ILogger<EpisodeMetadataProvider> logger)
            : base(httpClientFactory)
        {
            _apiClient = apiClient ?? throw new System.ArgumentNullException(nameof(apiClient));
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
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

            var seasons = await SeasonMetadataProvider.TryGetSeasons(_apiClient, _logger, seriesId, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var episode = FindEpisode(seasons, info.ParentIndexNumber.Value, info.IndexNumber.Value);

            if (episode == null)
                return result;

            var item = CreateEpisodeItem(episode, info.ParentIndexNumber.Value, seriesId);

            result.Item = item;
            result.HasMetadata = true;
            return result;
        }

        internal static JellyfinEpisode CreateEpisodeItem(EpisodeDto episode, int seasonNumber, int seriesId)
        {
            var item = new JellyfinEpisode
            {
                ParentIndexNumber = seasonNumber,
                IndexNumber = episode.Number,
                Name = !string.IsNullOrWhiteSpace(episode.Name) ? episode.Name : episode.EnName,
                Overview = episode.Description,
                PremiereDate = episode.AirDate?.Date
            };
            item.ProviderIds[Constants.ProviderId] = seriesId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return item;
        }

        internal static EpisodeDto FindEpisode(IEnumerable<SeasonDto> seasons, int seasonNumber, int episodeNumber)
            => seasons?
                .Where(season => season != null && season.Number == seasonNumber)
                .SelectMany(season => season.Episodes ?? new List<EpisodeDto>())
                .FirstOrDefault(item => item != null && item.Number == episodeNumber);

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
            => Task.FromResult(Enumerable.Empty<RemoteSearchResult>());
    }
}