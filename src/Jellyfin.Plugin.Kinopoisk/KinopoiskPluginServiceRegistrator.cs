using System.Net.Http;
using Jellyfin.Plugin.Kinopoisk.ProviderIdResolvers;
using Jellyfin.Plugin.Kinopoisk.MetadataProviders;
using Jellyfin.Plugin.Kinopoisk.Model;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Kinopoisk
{
    /// <summary>
    /// Registers services
    /// </summary>
    public class KinopoiskPluginServiceRegistrator : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            serviceCollection.AddHttpClient("KinopoiskApiClient", client =>
            {
                client.Timeout = System.TimeSpan.FromSeconds(20);
            }).AddHttpMessageHandler(() => new ApiKeyHandler(
                () => Plugin.Instance?.Configuration.ApiToken));

            serviceCollection.AddHttpClient("KinopoiskImageSanitizer", client =>
            {
                client.Timeout = System.TimeSpan.FromSeconds(10);
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false
            });

            serviceCollection.AddSingleton((sp) => new KinopoiskApiClient(
                Plugin.Instance?.Configuration.ApiToken,
                sp.GetRequiredService<ILogger<KinopoiskApiClient>>(),
                sp.GetRequiredService<IHttpClientFactory>()
            ));
            serviceCollection.AddSingleton<IKinopoiskApiClient>((sp) => new CachedKinopoiskApiClient(
                sp.GetRequiredService<KinopoiskApiClient>(),
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<ILogger<CachedKinopoiskApiClient>>()
            ));

            serviceCollection.AddSingleton<IProviderIdResolver<MovieInfo>, VideoResolver<MovieInfo>>();
            serviceCollection.AddSingleton<IProviderIdResolver<SeriesInfo>, VideoResolver<SeriesInfo>>();
            serviceCollection.AddSingleton<IProviderIdResolver<PersonLookupInfo>, CommonResolver<PersonLookupInfo>>();
            serviceCollection.AddSingleton<IProviderIdResolver<BaseItem>, CommonResolver<BaseItem>>();

            serviceCollection.AddSingleton<IMetadataProvider, MovieMetadataProvider>();
            serviceCollection.AddSingleton<IMetadataProvider, SeriesMetadataProvider>();
            serviceCollection.AddSingleton<IMetadataProvider, PersonMetadataProvider>();

            serviceCollection.AddSingleton<IRemoteImageProvider, VideoImageProvider>();
            serviceCollection.AddSingleton<IRemoteImageProvider, PersonImageProvider>();

            serviceCollection.AddSingleton<IExternalId, KinopoiskExternalId>();
            serviceCollection.AddSingleton<IExternalUrlProvider, KinopoiskExternalUrlProvider>();
        }
    }
}
