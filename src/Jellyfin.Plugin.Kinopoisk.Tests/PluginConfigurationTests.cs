using Jellyfin.Plugin.Kinopoisk.Configuration;
using Xunit;

namespace Jellyfin.Plugin.Kinopoisk.Tests
{
    public class PluginConfigurationTests
    {
        [Fact]
        public void GalleryOptionsAreDisabledByDefault()
        {
            var configuration = new PluginConfiguration();

            Assert.False(configuration.EnableAlternativePosters);
            Assert.False(configuration.EnableBackdropImages);
        }

        [Fact]
        public void GalleryOptionsCanBeEnabledIndependently()
        {
            var configuration = new PluginConfiguration
            {
                EnableAlternativePosters = true
            };

            Assert.True(configuration.EnableAlternativePosters);
            Assert.False(configuration.EnableBackdropImages);

            configuration.EnableBackdropImages = true;

            Assert.True(configuration.EnableAlternativePosters);
            Assert.True(configuration.EnableBackdropImages);
        }
    }
}
