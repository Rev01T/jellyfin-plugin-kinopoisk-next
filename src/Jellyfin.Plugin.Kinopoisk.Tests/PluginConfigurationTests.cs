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
    }
}