using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Kinopoisk.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        // Personal API key: https://kinopoiskapiunofficial.tech/profile
        public string ApiToken { get; set; } = string.Empty;

        // Gallery requests are opt-in to avoid unexpected API quota consumption.
        public bool EnableAlternativePosters { get; set; } = false;

        public bool EnableBackdropImages { get; set; } = false;
    }
}