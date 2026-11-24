using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Kinopoisk.Configuration;
using Jellyfin.Plugin.Kinopoisk.ProviderIdResolvers;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Kinopoisk.MetadataProviders
{
    public abstract class BaseVideoMetadataProvider<TItemType, TLookupInfoType> : BaseMetadataProvider, IRemoteMetadataProvider<TItemType, TLookupInfoType>
        where TItemType : BaseItem, IHasLookupInfo<TLookupInfoType>
        where TLookupInfoType : ItemLookupInfo, new()
    {
        private readonly ILogger _logger;
        protected readonly IKinopoiskApiClient _apiClient;
        private readonly IProviderIdResolver<TLookupInfoType> _providerIdResolver;

        public BaseVideoMetadataProvider(IKinopoiskApiClient kinopoiskApiClient, IProviderIdResolver<TLookupInfoType> providerIdResolver, ILogger logger, System.Net.Http.IHttpClientFactory httpClientFactory)
            : base(httpClientFactory)
        {
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
            _apiClient = kinopoiskApiClient ?? throw new System.ArgumentNullException(nameof(kinopoiskApiClient));
            _providerIdResolver = providerIdResolver ?? throw new System.ArgumentNullException(nameof(providerIdResolver));
        }

        protected abstract TItemType ConvertResponseToItem(MovieDto apiResponse);

        /// <summary>
        /// Which date is stored as the premiere date, according to the plugin setting.
        /// </summary>
        protected static PremiereDateSource PremiereDateSource
            => Plugin.Instance?.Configuration?.PremiereDateSource ?? PremiereDateSource.World;

        public async Task<MetadataResult<TItemType>> GetMetadata(TLookupInfoType info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<TItemType>()
            {
                QueriedById = true,
                Provider = Constants.ProviderName,
                ResultLanguage = Constants.ProviderMetadataLanguage
            };

            var (resolveResult, kinopoiskId) = await _providerIdResolver.TryResolve(info, cancellationToken);
            if (!resolveResult)
                return result;

            // One request: the card contains the cast and crew, the premiere dates and the poster too.
            var movie = await _apiClient.GetMovie(kinopoiskId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (movie is null)
                return result;

            result.Item = ConvertResponseToItem(movie);
            if (result.Item != null)
                result.HasMetadata = true;

            // The main data is already in hand. Failures of the steps below must not discard it
            // (title, overview, ratings...).
            var personInfos = movie.Persons.ToPersonInfos();
            var sanitizedPersons = await TryOptional(
                () => SanitizeEmptyImagePersonInfos(personInfos, cancellationToken),
                "person images",
                kinopoiskId,
                cancellationToken).ConfigureAwait(false);
            PersonPhotoCache.Remember(sanitizedPersons);
            foreach (var item in sanitizedPersons ?? Enumerable.Empty<PersonInfo>())
                result.AddPerson(item);

            // Trailers are not in the card: one more request, so it is optional.
            if (Plugin.Instance?.Configuration?.LoadTrailers == true && result.Item is not null)
            {
                var trailers = await TryOptional(
                    () => _apiClient.GetTrailers(kinopoiskId, cancellationToken),
                    "trailers",
                    kinopoiskId,
                    cancellationToken).ConfigureAwait(false);
                var remoteTrailers = trailers.ToMediaUrls();
                if (remoteTrailers is not null)
                    result.Item.RemoteTrailers = remoteTrailers;
            }

            return result;
        }

        /// <summary>
        /// Runs a request that only supplements the metadata. Any failure (quota, network,
        /// unexpected API response) is logged and replaced by <c>default</c>, so that the
        /// already obtained film data is still returned to Jellyfin.
        /// </summary>
        private async Task<T> TryOptional<T>(System.Func<Task<T>> action, string step, int kinopoiskId, CancellationToken cancellationToken)
        {
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A real cancellation (not an HttpClient timeout) must still stop the refresh.
                throw;
            }
            catch (System.Exception e)
            {
                // The API client has already reported the exhausted quota once; do not repeat it per item.
                var level = e is ApiException { IsQuotaExceeded: true } ? LogLevel.Debug : LogLevel.Warning;
                _logger.Log(
                    level,
                    "Skipping {Step} for Kinopoisk id {KinopoiskId}: {Error}",
                    step,
                    kinopoiskId,
                    e.GetBaseException().Message);
                return default;
            }
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(TLookupInfoType searchInfo, CancellationToken cancellationToken)
        {
            if (searchInfo.TryGetProviderId(Constants.ProviderId, out var kinopoiskIdStr)
                && int.TryParse(kinopoiskIdStr, out var kinopoiskId))
            {
                var providerIdResult = (await _apiClient.GetMovie(kinopoiskId, cancellationToken))
                    .ToRemoteSearchResult();
                return providerIdResult is null
                    ? Enumerable.Empty<RemoteSearchResult>()
                    : new[] { providerIdResult };
            }

            // Accept a plain Kinopoisk film ID (e.g. 687670) as well as the
            // optional filename-style forms kp687670 and kp-687670.
            if (TryParseKinopoiskId(searchInfo.Name, out kinopoiskId))
            {
                var idResult = (await _apiClient.GetMovie(kinopoiskId, cancellationToken))
                    .ToRemoteSearchResult();
                return idResult is null
                    ? Enumerable.Empty<RemoteSearchResult>()
                    : new[] { idResult };
            }

            if (string.IsNullOrWhiteSpace(searchInfo.Name))
                return Enumerable.Empty<RemoteSearchResult>();

            var searchResult = await _apiClient.SearchMovies(
                searchInfo.Name,
                cancellationToken: cancellationToken);
            return searchResult.ToRemoteSearchResults(_logger);
        }

        private static bool TryParseKinopoiskId(string value, out int kinopoiskId)
        {
            kinopoiskId = 0;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var candidate = value.Trim();
            if (candidate.StartsWith("kp-", System.StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(3);
            else if (candidate.StartsWith("kp", System.StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(2);

            return candidate.Length > 0
                && candidate.All(char.IsDigit)
                && int.TryParse(candidate, out kinopoiskId)
                && kinopoiskId > 0;
        }

        protected async Task<IEnumerable<PersonInfo>> SanitizeEmptyImagePersonInfos(IEnumerable<PersonInfo> images, CancellationToken cancellationToken)
        {
            var candidates = (images ?? Enumerable.Empty<PersonInfo>())
                .Where(person => person != null && !string.IsNullOrWhiteSpace(person.ImageUrl))
                .ToArray();
            if (candidates.Length == 0)
                return Enumerable.Empty<PersonInfo>();

            // The number of simultaneous requests is limited inside the shared sanitizer.
            var sanitizer = RemoteImageUrlSanitizer.CreateShared(_httpClientFactory.CreateClient("KinopoiskImageSanitizer"));

            var tasks = candidates.Select(async person =>
            {
                person.ImageUrl = await sanitizer.SanitizeRemoteImageUrl(person.ImageUrl, cancellationToken).ConfigureAwait(false);
                return person;
            });

            var sanitized = await Task.WhenAll(tasks).ConfigureAwait(false);
            return sanitized.Where(person => person != null && !string.IsNullOrWhiteSpace(person.ImageUrl)).ToArray();
        }
    }
}
