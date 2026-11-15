using System;
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
using JellyfinSeason = MediaBrowser.Controller.Entities.TV.Season;

namespace Jellyfin.Plugin.Kinopoisk.MetadataProviders
{
    /// <summary>
    /// Resolves season metadata from the Kinopoisk series seasons endpoint.
    /// </summary>
    public sealed class SeasonMetadataProvider : BaseMetadataProvider, IRemoteMetadataProvider<JellyfinSeason, SeasonInfo>
    {
        private readonly IKinopoiskApiClient _apiClient;
        private readonly ILogger<SeasonMetadataProvider> _logger;

        public SeasonMetadataProvider(IKinopoiskApiClient apiClient, IHttpClientFactory httpClientFactory, ILogger<SeasonMetadataProvider> logger)
            : base(httpClientFactory)
        {
            _apiClient = apiClient ?? throw new System.ArgumentNullException(nameof(apiClient));
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
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

            var response = await TryGetSeasons(_apiClient, _logger, seriesId, cancellationToken).ConfigureAwait(false);
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

        /// <summary>
        /// Loads the seasons of a series. A failure (quota, network, unexpected response) is logged
        /// and treated as "no data", so Jellyfin falls back to its other providers instead of
        /// reporting an error for every season and episode.
        /// </summary>
        internal static async Task<SeasonResponse> TryGetSeasons(IKinopoiskApiClient apiClient, ILogger logger, int seriesId, CancellationToken cancellationToken)
        {
            try
            {
                return await apiClient.GetSeasons(seriesId, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                // The API client has already reported the exhausted quota once; do not repeat it per item.
                var level = e is ApiException { StatusCode: 402 } ? LogLevel.Debug : LogLevel.Warning;
                logger.Log(level, "Skipping Kinopoisk seasons for series {SeriesId}: {Error}", seriesId, e.GetBaseException().Message);
                return null;
            }
        }

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