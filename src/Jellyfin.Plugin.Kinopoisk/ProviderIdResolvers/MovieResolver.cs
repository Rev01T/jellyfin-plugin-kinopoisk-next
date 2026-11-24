using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Kinopoisk.ProviderIdResolvers
{
    public class VideoResolver<T> : CommonLookupInfoResolver<T>
        where T : ItemLookupInfo
    {
        private readonly IKinopoiskApiClient _kinopoiskApiClient;

        public VideoResolver(IKinopoiskApiClient kinopoiskApiClient, ILogger<VideoResolver<T>> logger) : base(logger)
        {
            _kinopoiskApiClient = kinopoiskApiClient ?? throw new ArgumentNullException(nameof(kinopoiskApiClient));
        }

        public override async Task<(bool IsSuccess, int ProviderId)> TryResolve(T info, CancellationToken? ct = null)
        {
            var possibleResult = await base.TryResolve(info, ct);
            if (possibleResult.IsSuccess)
                return possibleResult;

            // Prefer the API's IMDb filter. This replaces one full-film request per
            // keyword candidate and therefore avoids N additional quota-consuming calls.
            if (info.TryGetProviderId(MetadataProvider.Imdb, out var imdbId) && !string.IsNullOrWhiteSpace(imdbId))
            {
                try
                {
                    _logger.LogDebug("Trying direct Kinopoisk lookup by IMDb ID '{ImdbId}'...", imdbId);
                    var imdbResult = await _kinopoiskApiClient.FindMoviesByImdbId(imdbId, ct);
                    if (imdbResult?.Count == 1)
                    {
                        var match = imdbResult.Single();
                        _logger.LogDebug("IMDb ID '{ImdbId}' resolved to Kinopoisk ID {KinopoiskId}", imdbId, match.Id);
                        return (true, match.Id);
                    }

                    if (imdbResult?.Count > 1)
                        _logger.LogWarning("IMDb lookup for '{ImdbId}' returned {Count} results; falling back to title search", imdbId, imdbResult.Count);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Direct Kinopoisk lookup by IMDb ID failed; falling back to title search");
                }
            }

            if (string.IsNullOrWhiteSpace(info.Name))
            {
                _logger.LogDebug("Film name is empty, skipping KinopoiskProviderId search");
                return (false, 0);
            }

            _logger.LogDebug("Trying to get suitable film with name '{Name}'...", info.Name);
            var searchResult = await _kinopoiskApiClient.SearchMovies(info.Name, 1, ct ?? CancellationToken.None);
            if (searchResult?.Docs is null || searchResult.Docs.Count < 1)
            {
                _logger.LogDebug("Received empty search result");
                return (false, 0);
            }

            var candidates = searchResult.Docs.Where(movie => movie != null).ToArray();
            _logger.LogDebug("Received {CandidateCount} results, trying to filter and match...", candidates.Length);
            var candidatesByYear = FilterByYear(info, candidates);

            possibleResult = await TryResolveBySingleCandidateLeft(info, candidatesByYear, ct);
            if (possibleResult.IsSuccess)
                return possibleResult;

            if (candidatesByYear.Count > 0)
            {
                var kinopoiskId = candidatesByYear.First().Id;
                _logger.LogDebug("All other checks failed, use first result by year, setting KinopoiskProviderId to {KinopoiskId} ({Name})", kinopoiskId, info.Name);
                return (true, kinopoiskId);
            }

            if (candidates.Length > 0)
            {
                var kinopoiskId = candidates.First().Id;
                _logger.LogDebug("All other checks failed, use first result, setting KinopoiskProviderId to {KinopoiskId} ({Name})", kinopoiskId, info.Name);
                return (true, kinopoiskId);
            }

            _logger.LogDebug("Suitable result not found");
            return (false, 0);
        }

        public Task<(bool IsSuccess, int ProviderId)> TryResolveBySingleCandidateLeft(T info, ICollection<MovieDto> candidates, CancellationToken? ct = null)
        {
            if (candidates.Count == 1)
            {
                var kinopoiskId = candidates.Single().Id;
                _logger.LogDebug("There is single candidate left, setting KinopoiskProviderId to {KinopoiskId} ({Name})", kinopoiskId, info.Name);
                return Task.FromResult((true, kinopoiskId));
            }

            return Task.FromResult((false, 0));
        }

        public ICollection<MovieDto> FilterByYear(T info, ICollection<MovieDto> candidates)
        {
            if (!info.Year.HasValue)
            {
                _logger.LogDebug("Can't filter by year, no year set in metadata");
                return Array.Empty<MovieDto>();
            }

            var targetYear = info.Year.Value;
            var result = candidates.Where(f => f.Year == targetYear).ToArray();
            _logger.LogDebug("Filtered by year {Year}, {Count} results left", targetYear, result.Length);
            return result;
        }
    }
}
