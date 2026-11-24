using System.Collections.Generic;
using System.Linq;
using KinopoiskUnofficialInfo.ApiClient;
using Xunit;

namespace Jellyfin.Plugin.Kinopoisk.Tests
{
    public class PersonPhotoCacheTests
    {
        [Fact]
        public void RemembersThePhotosOfACastListInTheOriginalSize()
        {
            // Ids that other tests do not use: the cache is shared by the process.
            var people = new[]
            {
                new PersonInMovieDto { Id = 910001, Name = "A", EnProfession = "actor", Photo = "https://example.test/get-kinopoisk-image/1/abc/300x300" },
                new PersonInMovieDto { Id = 910002, Name = "B", EnProfession = "actor", Photo = null },
                new PersonInMovieDto { Id = 910003, Name = "C", EnProfession = "actor", Photo = "https://example.test/get-kinopoisk-image/1/def/orig" }
            }.ToPersonInfos();

            PersonPhotoCache.Remember(people);

            Assert.True(PersonPhotoCache.TryGetOriginal(910001, out var url));
            Assert.Equal("https://example.test/get-kinopoisk-image/1/abc/orig", url);
            Assert.False(PersonPhotoCache.TryGetOriginal(910002, out _));
            Assert.True(PersonPhotoCache.TryGetOriginal(910003, out url));
            Assert.Equal("https://example.test/get-kinopoisk-image/1/def/orig", url);
            Assert.False(PersonPhotoCache.TryGetOriginal(910004, out _));
        }

        [Fact]
        public void IgnoresMissingInput()
        {
            PersonPhotoCache.Remember(null);
            PersonPhotoCache.Remember(new List<MediaBrowser.Controller.Entities.PersonInfo> { null, new MediaBrowser.Controller.Entities.PersonInfo { Name = "No id", ImageUrl = "https://example.test/x/300x300" } });
        }
    }
}
