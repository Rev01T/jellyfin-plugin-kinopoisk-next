namespace Jellyfin.Plugin.Kinopoisk.Configuration
{
    /// <summary>
    /// Which date is stored as the premiere date of a film or series.
    /// The dates are part of the title card, so no choice costs an extra API request.
    /// </summary>
    public enum PremiereDateSource
    {
        /// <summary>
        /// World premiere (default). If it is unknown, the earliest known date.
        /// </summary>
        World = 0,

        /// <summary>
        /// Release in Russian cinemas, falling back to the world premiere.
        /// </summary>
        Russia = 1,

        /// <summary>
        /// The premiere date is January 1 of the production year.
        /// </summary>
        YearOnly = 2
    }
}
