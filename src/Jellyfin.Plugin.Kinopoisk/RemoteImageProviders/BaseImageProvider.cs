using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Kinopoisk.MetadataProviders
{
    public abstract class BaseImageProvider : IRemoteImageProvider
    {
        private const int MaxConcurrentImageChecks = 8;
        private readonly IHttpClientFactory _httpClientFactory;

        protected BaseImageProvider(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public abstract string Name { get; }

        public abstract bool Supports(BaseItem item);

        public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return await _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken);
        }

        public abstract Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken);

        public abstract IEnumerable<ImageType> GetSupportedImages(BaseItem item);

        protected async Task<IEnumerable<RemoteImageInfo>> FilterEmptyImages(IEnumerable<RemoteImageInfo> images, CancellationToken cancellationToken)
        {
            using var semaphore = new SemaphoreSlim(MaxConcurrentImageChecks);
            var sanitizer = new RemoteImageUrlSanitizer(_httpClientFactory.CreateClient("KinopoiskImageSanitizer"));

            var tasks = images.Select(async image =>
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var sanitizedUrl = await sanitizer.SanitizeRemoteImageUrl(image.Url, cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(sanitizedUrl))
                        return null;

                    image.Url = sanitizedUrl;
                    return image;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var sanitized = await Task.WhenAll(tasks).ConfigureAwait(false);
            return sanitized.Where(image => image != null).ToArray();
        }
    }
}
