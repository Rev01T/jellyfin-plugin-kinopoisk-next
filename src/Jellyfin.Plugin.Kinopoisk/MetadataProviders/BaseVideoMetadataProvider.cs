using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private const int MaxConcurrentImageChecks = 8;
        private readonly ILogger _logger;
        private readonly IKinopoiskApiClient _apiClient;
        private readonly IProviderIdResolver<TLookupInfoType> _providerIdResolver;

        public BaseVideoMetadataProvider(IKinopoiskApiClient kinopoiskApiClient, IProviderIdResolver<TLookupInfoType> providerIdResolver, ILogger logger, System.Net.Http.IHttpClientFactory httpClientFactory)
            : base(httpClientFactory)
        {
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
            _apiClient = kinopoiskApiClient ?? throw new System.ArgumentNullException(nameof(kinopoiskApiClient));
            _providerIdResolver = providerIdResolver ?? throw new System.ArgumentNullException(nameof(providerIdResolver));
        }

        protected abstract TItemType ConvertResponseToItem(Film apiResponse);

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

            var film = await _apiClient.GetSingleFilm(kinopoiskId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (film is null)
                return result;

            result.Item = ConvertResponseToItem(film);
            if (result.Item != null)
                result.HasMetadata = true;

            var staff = await _apiClient.GetStaff(kinopoiskId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var personInfos = staff?.ToPersonInfos() ?? Enumerable.Empty<PersonInfo>();
            var sanitizedPersons = await SanitizeEmptyImagePersonInfos(personInfos, cancellationToken);
            foreach (var item in sanitizedPersons)
                result.AddPerson(item);

            var trailers = await _apiClient.GetTrailers(kinopoiskId, cancellationToken);
            var remoteTrailers = trailers.ToMediaUrls();
            if (remoteTrailers is not null && result.Item is not null)
                result.Item.RemoteTrailers = remoteTrailers;

            return result;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(TLookupInfoType searchInfo, CancellationToken cancellationToken)
        {
            if (searchInfo.TryGetProviderId(Constants.ProviderId, out var kinopoiskIdStr)
                && int.TryParse(kinopoiskIdStr, out var kinopoiskId))
            {
                var singleResult = (await _apiClient.GetSingleFilm(kinopoiskId, cancellationToken)).ToRemoteSearchResult();
                return Enumerable.Repeat(singleResult, 1);
            }

            return (await _apiClient.SearchByKeyword(searchInfo.Name, cancellationToken: cancellationToken)).ToRemoteSearchResults(_logger);
        }

        protected async Task<IEnumerable<PersonInfo>> SanitizeEmptyImagePersonInfos(IEnumerable<PersonInfo> images, CancellationToken cancellationToken)
        {
            var candidates = (images ?? Enumerable.Empty<PersonInfo>())
                .Where(person => person != null && !string.IsNullOrWhiteSpace(person.ImageUrl))
                .ToArray();
            if (candidates.Length == 0)
                return Enumerable.Empty<PersonInfo>();

            using var semaphore = new SemaphoreSlim(MaxConcurrentImageChecks);
            var sanitizer = new RemoteImageUrlSanitizer(_httpClientFactory.CreateClient("KinopoiskImageSanitizer"));

            var tasks = candidates.Select(async person =>
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    person.ImageUrl = await sanitizer.SanitizeRemoteImageUrl(person.ImageUrl, cancellationToken).ConfigureAwait(false);
                    return person;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var sanitized = await Task.WhenAll(tasks).ConfigureAwait(false);
            return sanitized.Where(person => person != null && !string.IsNullOrWhiteSpace(person.ImageUrl)).ToArray();
        }
    }
}
