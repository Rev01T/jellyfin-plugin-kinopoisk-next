using System;
using System.Linq;
using Jellyfin.Plugin.Kinopoisk.Configuration;
using KinopoiskUnofficialInfo.ApiClient;

namespace Jellyfin.Plugin.Kinopoisk
{
    /// <summary>
    /// Chooses the premiere date of a title from the dates in its card.
    /// </summary>
    public static class PremiereDateSelector
    {
        /// <summary>
        /// The premiere date according to the plugin setting. Null when the setting is
        /// <see cref="PremiereDateSource.YearOnly"/> or the card has no premiere date: a date is never
        /// made up from the year (it would show as January 1). The year is stored separately, in the
        /// production year of the item.
        /// </summary>
        public static DateTime? Select(MovieDto movie, PremiereDateSource source)
        {
            if (movie is null)
                return null;

            if (source == PremiereDateSource.YearOnly || movie.Premiere is null)
                return null;

            var date = source == PremiereDateSource.Russia
                ? SelectRussian(movie.Premiere) ?? SelectWorld(movie.Premiere)
                : SelectWorld(movie.Premiere);
            return date?.Date;
        }

        // The world premiere, or the earliest known date when it is missing. Re-releases are ignored:
        // they say nothing about the first release.
        private static DateTime? SelectWorld(PremiereDto premiere)
        {
            if (premiere.World.HasValue)
                return premiere.World;

            return new[] { premiere.Russia, premiere.Usa, premiere.Cinema, premiere.Digital, premiere.Bluray, premiere.Dvd }
                .Where(date => date.HasValue)
                .OrderBy(date => date.Value)
                .FirstOrDefault();
        }

        // The release in Russian cinemas.
        private static DateTime? SelectRussian(PremiereDto premiere)
            => premiere.Russia;
    }
}
