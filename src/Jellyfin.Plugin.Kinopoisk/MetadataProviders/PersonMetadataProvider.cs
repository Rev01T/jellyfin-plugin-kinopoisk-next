using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Kinopoisk.ProviderIdResolvers;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Kinopoisk.MetadataProviders
{
    public class PersonMetadataProvider : BaseMetadataProvider, IRemoteMetadataProvider<Person, PersonLookupInfo>
    {
        private readonly IKinopoiskApiClient _apiClient;
        private readonly IProviderIdResolver<PersonLookupInfo> _providerIdResolver;
        private readonly ILogger<PersonMetadataProvider> _logger;

        public PersonMetadataProvider(IKinopoiskApiClient apiClient, IProviderIdResolver<PersonLookupInfo> providerIdResolver, ILogger<PersonMetadataProvider> logger, IHttpClientFactory httpClientFactory)
            : base(httpClientFactory)
        {
            _apiClient = apiClient ?? throw new System.ArgumentNullException(nameof(apiClient));
            _providerIdResolver = providerIdResolver ?? throw new System.ArgumentNullException(nameof(providerIdResolver));
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
        }

        public async Task<MetadataResult<Person>> GetMetadata(PersonLookupInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Person>()
            {
                QueriedById = true,
                Provider = Constants.ProviderName,
                ResultLanguage = Constants.ProviderMetadataLanguage
            };

            // The details of a person (birth date and place) cost one API request per person, which
            // the daily quota of a free token cannot afford for a whole cast. Off by default.
            if (Plugin.Instance?.Configuration?.LoadPersonDetails != true)
                return result;

            var (resolveResult, kinopoiskId) = await _providerIdResolver.TryResolve(info, cancellationToken);
            if (!resolveResult)
                return result;

            PersonDto person;
            try
            {
                person = await _apiClient.GetPerson(kinopoiskId, cancellationToken);
            }
            catch (ApiException e) when (e.IsQuotaExceeded)
            {
                _logger.LogDebug("Details of the person {KinopoiskId} were not loaded: {Error}", kinopoiskId, e.Message);
                return result;
            }

            cancellationToken.ThrowIfCancellationRequested();

            result.Item = person.ToPerson();
            if (result.Item != null)
                result.HasMetadata = true;

            return result;
        }

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(PersonLookupInfo searchInfo, CancellationToken cancellationToken)
            => Task.FromResult(Enumerable.Empty<RemoteSearchResult>()); // Not supported
    }
}
