using Jellyfin.Plugin.Kinopoisk.Configuration;
using Xunit;

namespace Jellyfin.Plugin.Kinopoisk.Tests
{
    public class PluginConfigurationTests
    {
        [Fact]
        public void ApiTokenIsEmptyByDefault()
        {
            var configuration = new PluginConfiguration();

            Assert.Equal(string.Empty, configuration.ApiToken);
        }

        [Fact]
        public void ExtraRequestsAreOffByDefault()
        {
            // The free token has a daily limit of 200 requests: only the card of a title is requested by default.
            var configuration = new PluginConfiguration();

            Assert.False(configuration.LoadPersonDetails);
            Assert.Equal(PremiereDateSource.World, configuration.PremiereDateSource);
        }
    }
}
