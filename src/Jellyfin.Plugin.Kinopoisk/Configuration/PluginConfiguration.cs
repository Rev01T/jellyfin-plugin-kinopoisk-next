using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Kinopoisk.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        // Personal API key: https://kinopoiskapiunofficial.tech/profile
        public string ApiToken { get; set; } = string.Empty;

        // Source of the premiere date for films and series.
        public PremiereDateSource PremiereDateSource { get; set; } = PremiereDateSource.World;
    }
}