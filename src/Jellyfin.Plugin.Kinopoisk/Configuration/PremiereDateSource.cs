namespace Jellyfin.Plugin.Kinopoisk.Configuration
{
    /// <summary>
    /// Which date is stored as the premiere date of a film or series.
    /// </summary>
    public enum PremiereDateSource
    {
        /// <summary>
        /// World premiere from the Kinopoisk distributions (default). Costs one extra API request per item.
        /// </summary>
        World = 0,

        /// <summary>
        /// Russian release (cinema first), falling back to the world premiere.
        /// Costs one extra API request per item.
        /// </summary>
        Russia = 1,

        /// <summary>
        /// Do not request distributions: the premiere date is January 1 of the production year.
        /// </summary>
        YearOnly = 2
    }
}