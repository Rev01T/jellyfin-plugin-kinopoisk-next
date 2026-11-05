using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Kinopoisk.ProviderIdResolvers;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Kinopoisk.MetadataProviders
{
    public class VideoImageProvider : BaseImageProvider
    {
        private readonly ILogger<VideoImageProvider> _logger;
        private readonly IKinopoiskApiClient _apiClient;
        private readonly IProviderIdResolver<BaseItem> _providerIdResolver;

        public override string Name => Constants.ProviderName;

        public VideoImageProvider(IKinopoiskApiClient kinopoiskApiClient, IProviderIdResolver<BaseItem> providerIdResolver, ILogger<VideoImageProvider> logger, IHttpClientFactory httpClientFactory)
            : base(httpClientFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiClient = kinopoiskApiClient ?? throw new ArgumentNullException(nameof(kinopoiskApiClient));
            _providerIdResolver = providerIdResolver ?? throw new ArgumentNullException(nameof(providerIdResolver));
        }

        public override bool Supports(BaseItem item)
            => item is Movie || item is Series;

        public override IEnumerable<ImageType> GetSupportedImages(BaseItem item)
            => new[] { ImageType.Primary, ImageType.Backdrop };

        public override async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var (resolveResult, kinopoiskId) = await _providerIdResolver.TryResolve(item, cancellationToken);
            if (!resolveResult)
                return Enumerable.Empty<RemoteImageInfo>();

            // Keep the film's posterUrl first: the API exposes it as the current film poster.
            // The images endpoint supplies additional full-size alternatives.
            var film = await _apiClient.GetSingleFilm(kinopoiskId, cancellationToken);
            var images = new List<RemoteImageInfo>();
            images.AddRange(film.ToRemoteImageInfos());

            var posters = await _apiClient.GetImages(kinopoiskId, "POSTER", cancellationToken: cancellationToken);
            images.AddRange(posters.ToRemoteImageInfos(ImageType.Primary));

            var wallpapers = await _apiClient.GetImages(kinopoiskId, "WALLPAPER", cancellationToken: cancellationToken);
            var backdropImages = wallpapers.ToRemoteImageInfos(ImageType.Backdrop).ToArray();
            if (backdropImages.Length == 0)
            {
                var covers = await _apiClient.GetImages(kinopoiskId, "COVER", cancellationToken: cancellationToken);
                backdropImages = covers.ToRemoteImageInfos(ImageType.Backdrop).ToArray();
            }
            images.AddRange(backdropImages);

            var uniqueImages = images
                .Where(image => image != null && !string.IsNullOrWhiteSpace(image.Url))
                .GroupBy(image => (image.Type, image.Url), StringTupleComparer.Instance)
                .Select(group => group.First())
                .ToArray();

            _logger.LogDebug(
                "Retrieved {PrimaryCount} poster and {BackdropCount} backdrop candidates for Kinopoisk film {KinopoiskId}",
                uniqueImages.Count(image => image.Type == ImageType.Primary),
                uniqueImages.Count(image => image.Type == ImageType.Backdrop),
                kinopoiskId);

            return await FilterEmptyImages(uniqueImages, cancellationToken);
        }

        private sealed class StringTupleComparer : IEqualityComparer<(ImageType Type, string Url)>
        {
            public static readonly StringTupleComparer Instance = new StringTupleComparer();

            public bool Equals((ImageType Type, string Url) x, (ImageType Type, string Url) y)
                => x.Type == y.Type && StringComparer.OrdinalIgnoreCase.Equals(x.Url, y.Url);

            public int GetHashCode((ImageType Type, string Url) obj)
                => HashCode.Combine(obj.Type, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Url ?? string.Empty));
        }
    }
}
