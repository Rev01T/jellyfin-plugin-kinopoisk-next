using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.Kinopoisk.Configuration;
using KinopoiskUnofficialInfo.ApiClient;

namespace Jellyfin.Plugin.Kinopoisk
{
    /// <summary>
    /// Chooses the premiere date from the Kinopoisk film distributions.
    /// </summary>
    public static class DistributionPremiereDateSelector
    {
        private const string Russia = "Россия";

        public static DateTime? Select(DistributionResponse src, PremiereDateSource source)
        {
            if (src?.Items is null || source == PremiereDateSource.YearOnly)
                return null;

            if (source == PremiereDateSource.Russia)
            {
                var russian = SelectRussian(src.Items);
                if (russian.HasValue)
                    return russian;
            }

            return SelectWorld(src.Items);
        }

        private static DateTime? SelectWorld(IEnumerable<Distribution> items)
        {
            var worldPremiere = items
                .Where(distribution => distribution != null && distribution.Type == DistributionType.WORLD_PREMIER)
                .Select(distribution => distribution.Date.ParseDate())
                .Where(date => date.HasValue)
                .OrderBy(date => date.Value)
                .FirstOrDefault();

            if (worldPremiere.HasValue)
                return worldPremiere;

            return items
                .Where(distribution => distribution != null && distribution.Type == DistributionType.PREMIERE)
                .Select(distribution => distribution.Date.ParseDate())
                .Where(date => date.HasValue)
                .OrderBy(date => date.Value)
                .FirstOrDefault();
        }

        private static DateTime? SelectRussian(IEnumerable<Distribution> items)
        {
            // Re-releases are skipped: they say nothing about the first Russian release.
            var dated = items
                .Where(distribution => distribution != null
                    && !distribution.ReRelease
                    && distribution.Type != DistributionType.ALL
                    && string.Equals(distribution.Country?.Country1, Russia, StringComparison.OrdinalIgnoreCase))
                .Select(distribution => new
                {
                    distribution.SubType,
                    Date = distribution.Date.ParseDate()
                })
                .Where(distribution => distribution.Date.HasValue)
                .ToList();

            // Cinema release first; DVD, digital and Blu-ray dates only when there is no cinema one.
            var cinema = dated
                .Where(distribution => distribution.SubType == DistributionSubType.CINEMA)
                .Select(distribution => distribution.Date)
                .OrderBy(date => date.Value)
                .FirstOrDefault();

            if (cinema.HasValue)
                return cinema;

            return dated
                .Select(distribution => distribution.Date)
                .OrderBy(date => date.Value)
                .FirstOrDefault();
        }
    }
}