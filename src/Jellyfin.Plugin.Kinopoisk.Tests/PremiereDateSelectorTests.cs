using System;
using Jellyfin.Plugin.Kinopoisk.Configuration;
using KinopoiskUnofficialInfo.ApiClient;
using Xunit;

namespace Jellyfin.Plugin.Kinopoisk.Tests
{
    public class PremiereDateSelectorTests
    {
        private static DateTime Utc(int year, int month, int day) => new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);

        private static MovieDto Movie(PremiereDto premiere, int? year = 2020)
            => new MovieDto { Year = year, Premiere = premiere };

        [Fact]
        public void WorldSourceUsesWorldPremiere()
        {
            var movie = Movie(new PremiereDto { World = Utc(2020, 1, 10), Russia = Utc(2020, 2, 20) });

            Assert.Equal(new DateTime(2020, 1, 10), PremiereDateSelector.Select(movie, PremiereDateSource.World));
        }

        [Fact]
        public void WorldSourceFallsBackToTheEarliestKnownDate()
        {
            var movie = Movie(new PremiereDto { Digital = Utc(2020, 5, 1), Russia = Utc(2020, 3, 1), Dvd = Utc(2020, 8, 1) });

            Assert.Equal(new DateTime(2020, 3, 1), PremiereDateSelector.Select(movie, PremiereDateSource.World));
        }

        [Fact]
        public void ReReleaseIsNeverUsed()
        {
            var movie = Movie(new PremiereDto { Rerelease = Utc(2019, 1, 1), Russia = Utc(2020, 3, 1) });

            Assert.Equal(new DateTime(2020, 3, 1), PremiereDateSelector.Select(movie, PremiereDateSource.World));
            Assert.Null(PremiereDateSelector.Select(Movie(new PremiereDto { Rerelease = Utc(2019, 1, 1) }), PremiereDateSource.World));
        }

        [Fact]
        public void RussiaSourceUsesTheRussianRelease()
        {
            var movie = Movie(new PremiereDto { World = Utc(2020, 1, 10), Russia = Utc(2020, 2, 20) });

            Assert.Equal(new DateTime(2020, 2, 20), PremiereDateSelector.Select(movie, PremiereDateSource.Russia));
        }

        [Fact]
        public void RussiaSourceFallsBackToTheWorldPremiere()
        {
            var movie = Movie(new PremiereDto { World = Utc(2020, 1, 10), Digital = Utc(2020, 6, 1) });

            Assert.Equal(new DateTime(2020, 1, 10), PremiereDateSelector.Select(movie, PremiereDateSource.Russia));
        }

        [Fact]
        public void YearOnlySourceLeavesTheDateEmpty()
        {
            // Only the production year is stored: a date made up as January 1 would be wrong.
            var movie = Movie(new PremiereDto { World = Utc(2019, 12, 20) }, 2020);

            Assert.Null(PremiereDateSelector.Select(movie, PremiereDateSource.YearOnly));
        }

        [Fact]
        public void LeavesTheDateEmptyWithoutPremiereDates()
        {
            Assert.Null(PremiereDateSelector.Select(Movie(null), PremiereDateSource.World));
            Assert.Null(PremiereDateSelector.Select(Movie(new PremiereDto()), PremiereDateSource.Russia));
        }

        [Fact]
        public void IsMissingWithoutAnyDate()
        {
            Assert.Null(PremiereDateSelector.Select(Movie(null, null), PremiereDateSource.World));
            Assert.Null(PremiereDateSelector.Select(Movie(null, 0), PremiereDateSource.World));
            Assert.Null(PremiereDateSelector.Select(null, PremiereDateSource.World));
        }
    }
}
