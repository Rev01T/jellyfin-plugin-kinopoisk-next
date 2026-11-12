using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Kinopoisk.Configuration;
using KinopoiskUnofficialInfo.ApiClient;
using Xunit;

namespace Jellyfin.Plugin.Kinopoisk.Tests
{
    public class DistributionPremiereDateSourceTests
    {
        private static Distribution Item(DistributionType type, DistributionSubType subType, string country, string date, bool reRelease = false)
            => new Distribution
            {
                Type = type,
                SubType = subType,
                Country = new Country { Country1 = country },
                Date = date,
                ReRelease = reRelease
            };

        private static DistributionResponse Response(params Distribution[] items)
            => new DistributionResponse { Items = new List<Distribution>(items) };

        [Fact]
        public void WorldSourceUsesWorldPremiere()
        {
            var distributions = Response(
                Item(DistributionType.WORLD_PREMIER, DistributionSubType.CINEMA, "США", "2020-01-10"),
                Item(DistributionType.PREMIERE, DistributionSubType.CINEMA, "Россия", "2020-02-20"));

            Assert.Equal(new DateTime(2020, 1, 10), distributions.GetPremiereDate(PremiereDateSource.World));
        }

        [Fact]
        public void RussiaSourceUsesRussianCinemaRelease()
        {
            var distributions = Response(
                Item(DistributionType.WORLD_PREMIER, DistributionSubType.CINEMA, "США", "2020-01-10"),
                Item(DistributionType.PREMIERE, DistributionSubType.DIGITAL, "Россия", "2020-05-01"),
                Item(DistributionType.PREMIERE, DistributionSubType.CINEMA, "Россия", "2020-02-20"));

            Assert.Equal(new DateTime(2020, 2, 20), distributions.GetPremiereDate(PremiereDateSource.Russia));
        }

        [Fact]
        public void RussiaSourceUsesRussianHomeMediaWhenThereIsNoCinemaRelease()
        {
            var distributions = Response(
                Item(DistributionType.WORLD_PREMIER, DistributionSubType.CINEMA, "США", "2020-01-10"),
                Item(DistributionType.PREMIERE, DistributionSubType.DVD, "Россия", "2020-06-01"),
                Item(DistributionType.PREMIERE, DistributionSubType.DIGITAL, "Россия", "2020-05-01"));

            Assert.Equal(new DateTime(2020, 5, 1), distributions.GetPremiereDate(PremiereDateSource.Russia));
        }

        [Fact]
        public void RussiaSourceIgnoresReReleases()
        {
            var distributions = Response(
                Item(DistributionType.WORLD_PREMIER, DistributionSubType.CINEMA, "США", "2020-01-10"),
                Item(DistributionType.PREMIERE, DistributionSubType.CINEMA, "Россия", "2025-03-01", reRelease: true));

            Assert.Equal(new DateTime(2020, 1, 10), distributions.GetPremiereDate(PremiereDateSource.Russia));
        }

        [Fact]
        public void RussiaSourceFallsBackToWorldWhenThereIsNoRussianDate()
        {
            var distributions = Response(
                Item(DistributionType.WORLD_PREMIER, DistributionSubType.CINEMA, "США", "2020-01-10"),
                Item(DistributionType.PREMIERE, DistributionSubType.CINEMA, "Франция", "2020-02-20"));

            Assert.Equal(new DateTime(2020, 1, 10), distributions.GetPremiereDate(PremiereDateSource.Russia));
        }

        [Fact]
        public void RussiaSourceToleratesMissingCountryAndUnknownSubType()
        {
            var withoutCountry = new Distribution { Type = DistributionType.PREMIERE, Date = "2020-02-20", Country = null };
            var unknownSubType = Item(DistributionType.PREMIERE, (DistributionSubType)(-1), "Россия", "2020-03-01");
            var distributions = Response(withoutCountry, unknownSubType);

            Assert.Equal(new DateTime(2020, 3, 1), distributions.GetPremiereDate(PremiereDateSource.Russia));
        }

        [Fact]
        public void YearOnlySourceReturnsNoDate()
        {
            var distributions = Response(
                Item(DistributionType.WORLD_PREMIER, DistributionSubType.CINEMA, "США", "2020-01-10"));

            Assert.Null(distributions.GetPremiereDate(PremiereDateSource.YearOnly));
        }

        [Fact]
        public void NullResponseReturnsNoDate()
        {
            Assert.Null(((DistributionResponse)null).GetPremiereDate(PremiereDateSource.Russia));
        }
    }
}