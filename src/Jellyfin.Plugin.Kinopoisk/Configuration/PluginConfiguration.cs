using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Kinopoisk.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        // Personal API key: https://kinopoiskapiunofficial.tech/profile
        public string ApiToken { get; set; } = string.Empty;
    }
}