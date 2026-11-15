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
            var candidates = (images ?? Enumerable.Empty<RemoteImageInfo>())
                .Where(image => image != null && !string.IsNullOrWhiteSpace(image.Url))
                .ToArray();
            if (candidates.Length == 0)
                return Enumerable.Empty<RemoteImageInfo>();

            // The number of simultaneous requests is limited inside the shared sanitizer.
            var sanitizer = RemoteImageUrlSanitizer.CreateShared(_httpClientFactory.CreateClient("KinopoiskImageSanitizer"));

            var tasks = candidates.Select(async image =>
            {
                var sanitizedUrl = await sanitizer.SanitizeRemoteImageUrl(image.Url, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(sanitizedUrl))
                    return null;

                image.Url = sanitizedUrl;
                return image;
            });

            var sanitized = await Task.WhenAll(tasks).ConfigureAwait(false);
            return sanitized.Where(image => image != null).ToArray();
        }
    }
}
