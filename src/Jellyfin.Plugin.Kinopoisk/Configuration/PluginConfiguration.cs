using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Kinopoisk.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        // Personal API token: https://t.me/poiskkinodev_bot (https://poiskkino.dev)
        public string ApiToken { get; set; } = string.Empty;

        // Source of the premiere date for films and series.
        public PremiereDateSource PremiereDateSource { get; set; } = PremiereDateSource.World;

        // Request the details of every person (birth date and place): one API request per person.
        public bool LoadPersonDetails { get; set; } = false;
    }
}
