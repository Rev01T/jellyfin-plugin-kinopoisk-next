using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Kinopoisk.MetadataProviders;
using Jellyfin.Plugin.Kinopoisk.ProviderIdResolvers;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Kinopoisk.Tests
{
    /// <summary>
    /// The free token allows 200 requests a day, so the number of requests per title matters.
    /// </summary>
    public class MovieMetadataProviderTests
    {
        private sealed class FixedIdResolver<T> : IProviderIdResolver<T>
            where T : MediaBrowser.Model.Entities.IHasProviderIds
        {
            private readonly int _id;

            public FixedIdResolver(int id) => _id = id;

            public Task<(bool IsSuccess, int ProviderId)> TryResolve(T info, CancellationToken? ct = null)
                => Task.FromResult((true, _id));
        }

        [Fact]
        public async Task AFilmIsLoadedWithASingleApiRequest()
        {
            var api = new FakeApiClient { Movie = Fixtures.Movie };
            var provider = new MovieMetadataProvider(
                api, new FixedIdResolver<MovieInfo>(251733), NullLogger<MovieMetadataProvider>.Instance, new FakeHttpClientFactory());

            var result = await provider.GetMetadata(new MovieInfo(), CancellationToken.None);

            Assert.True(result.HasMetadata);
            Assert.Equal("Аватар", result.Item.Name);
            // Cast, crew and premiere dates come with the card: no more requests, no trailers, no people.
            Assert.Equal(1, api.TotalRequests);
            Assert.Equal(1, api.MovieRequests);
            // 88 people in the card, 14 of them dubbing actors, 17 more without a photo.
            var expectedPeople = Fixtures.Movie(251733).Persons
                .Count(person => person.EnProfession != "voiceover" && !string.IsNullOrWhiteSpace(person.Photo));
            Assert.Equal(57, expectedPeople);
            Assert.Equal(expectedPeople, result.People.Count);
            Assert.Equal(PersonKind.Actor, result.People[0].Type);
            Assert.Equal(new System.DateTime(2009, 12, 10), result.Item.PremiereDate);
        }

        [Fact]
        public async Task ASeriesIsLoadedWithASingleApiRequest()
        {
            var api = new FakeApiClient { Movie = Fixtures.Movie };
            var provider = new SeriesMetadataProvider(
                api, new FixedIdResolver<SeriesInfo>(77298), NullLogger<SeriesMetadataProvider>.Instance, new FakeHttpClientFactory());

            var result = await provider.GetMetadata(new SeriesInfo(), CancellationToken.None);

            Assert.True(result.HasMetadata);
            Assert.Equal("Вавилон 5", result.Item.Name);
            Assert.Equal(1, api.TotalRequests);
        }

        [Fact]
        public async Task PeopleWithoutAPhotoAreNotAdded()
        {
            // Kept from the previous version of the plugin: only people with a working photo are added.
            var api = new FakeApiClient { Movie = Fixtures.Movie };
            var provider = new MovieMetadataProvider(
                api, new FixedIdResolver<MovieInfo>(1395460), NullLogger<MovieMetadataProvider>.Instance, new FakeHttpClientFactory());

            var result = await provider.GetMetadata(new MovieInfo(), CancellationToken.None);

            var withoutPhoto = Fixtures.Movie(1395460).Persons
                .Count(person => string.IsNullOrWhiteSpace(person.Photo) && person.EnProfession != "voiceover");
            Assert.True(withoutPhoto > 0);
            Assert.Equal(30 - withoutPhoto, result.People.Count);
        }

        [Fact]
        public async Task ATitleThatDoesNotExistGivesNoMetadata()
        {
            var api = new FakeApiClient();
            var provider = new MovieMetadataProvider(
                api, new FixedIdResolver<MovieInfo>(5), NullLogger<MovieMetadataProvider>.Instance, new FakeHttpClientFactory());

            var result = await provider.GetMetadata(new MovieInfo(), CancellationToken.None);

            Assert.False(result.HasMetadata);
            Assert.Null(result.Item);
        }

        [Fact]
        public async Task PersonProviderMakesNoRequestsByDefault()
        {
            var api = new FakeApiClient { Person = _ => new PersonDto { Id = 1, Name = "Тест" } };
            var provider = new PersonMetadataProvider(
                api, new FixedIdResolver<PersonLookupInfo>(3873197), NullLogger<PersonMetadataProvider>.Instance, new FakeHttpClientFactory());

            var result = await provider.GetMetadata(new PersonLookupInfo(), CancellationToken.None);

            Assert.False(result.HasMetadata);
            Assert.Equal(0, api.PersonRequests);
        }

        [Fact]
        public async Task PersonImageComesFromTheCastListWithoutARequest()
        {
            var api = new FakeApiClient { Movie = Fixtures.Movie };
            var movieProvider = new MovieMetadataProvider(
                api, new FixedIdResolver<MovieInfo>(1395460), NullLogger<MovieMetadataProvider>.Instance, new FakeHttpClientFactory());
            await movieProvider.GetMetadata(new MovieInfo(), CancellationToken.None);

            var imageProvider = new PersonImageProvider(
                api,
                new FixedIdResolver<MediaBrowser.Controller.Entities.BaseItem>(1769503),
                NullLogger<PersonImageProvider>.Instance,
                new FakeHttpClientFactory());

            var images = (await imageProvider.GetImages(new MediaBrowser.Controller.Entities.Person(), CancellationToken.None)).ToArray();

            var image = Assert.Single(images);
            Assert.EndsWith("/orig", image.Url);
            Assert.Equal(1, api.TotalRequests);
            Assert.Equal(0, api.PersonRequests);
        }
    }
}
