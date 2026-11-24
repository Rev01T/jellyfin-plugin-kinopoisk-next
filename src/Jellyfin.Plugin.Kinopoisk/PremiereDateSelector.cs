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
        /// The premiere date according to the plugin setting. When the card has no suitable date
        /// (or the setting is <see cref="PremiereDateSource.YearOnly"/>), it is January 1 of the production year.
        /// </summary>
        public static DateTime? Select(MovieDto movie, PremiereDateSource source)
        {
            if (movie is null)
                return null;

            if (source != PremiereDateSource.YearOnly && movie.Premiere != null)
            {
                var date = source == PremiereDateSource.Russia
                    ? SelectRussian(movie.Premiere) ?? SelectWorld(movie.Premiere)
                    : SelectWorld(movie.Premiere);
                if (date.HasValue)
                    return date.Value.Date;
            }

            return movie.Year > 1900 ? new DateTime(movie.Year.Value, 1, 1) : (DateTime?)null;
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
