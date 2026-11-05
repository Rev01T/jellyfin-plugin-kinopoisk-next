using System.Collections.Generic;
using System.Linq;
using KinopoiskUnofficialInfo.ApiClient;
using Xunit;

namespace Jellyfin.Plugin.Kinopoisk.Tests
{
    public class ApiModelExtensionsTests
    {
        [Fact]
        public void ToPersonInfosShouldSkipPersonsWithoutNames()
        {
            var staff = new List<StaffResponse>
            {
                CreateStaff(1, "Иван Иванов", string.Empty),
                CreateStaff(2, string.Empty, string.Empty),
                CreateStaff(3, "   ", "   "),
                CreateStaff(4, string.Empty, "John Smith")
            };

            var result = staff.ToPersonInfos().ToArray();

            Assert.Equal(2, result.Length);
            Assert.Equal("Иван Иванов", result[0].Name);
            Assert.Equal(1, result[0].SortOrder);
            Assert.Equal("John Smith", result[1].Name);
            Assert.Equal(2, result[1].SortOrder);
        }

        [Fact]
        public void ToRemoteImageInfosUsesOriginalImageUrlInsteadOfPreviewUrl()
        {
            var response = new FilmImagesResponse
            {
                Items = new List<FilmImage>
                {
                    new FilmImage
                    {
                        ImageUrl = "https://images.example/original",
                        PreviewUrl = "https://images.example/300x"
                    },
                    new FilmImage
                    {
                        ImageUrl = null,
                        PreviewUrl = "https://images.example/preview-only"
                    }
                }
            };

            var result = response.ToRemoteImageInfos(MediaBrowser.Model.Entities.ImageType.Primary).ToArray();

            Assert.Single(result);
            Assert.Equal("https://images.example/original", result[0].Url);
            Assert.Equal(MediaBrowser.Model.Entities.ImageType.Primary, result[0].Type);
        }

        private static StaffResponse CreateStaff(
            int id,
            string nameRu,
            string nameEn)
        {
            return new StaffResponse
            {
                StaffId = id,
                NameRu = nameRu,
                NameEn = nameEn,
                PosterUrl = string.Empty,
                ProfessionText = "Актёр",
                ProfessionKey = StaffResponseProfessionKey.ACTOR
            };
        }
    }
}
