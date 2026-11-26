using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.Kinopoisk.Configuration;
using KinopoiskUnofficialInfo.ApiClient;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using Jellyfin.Data.Enums;
using Jellyfin.Extensions;

namespace Jellyfin.Plugin.Kinopoisk
{
    public static class ApiModelExtensions
    {
        public static RemoteSearchResult ToRemoteSearchResult(this MovieDto src)
        {
            if (src is null)
                return null;

            var res = new RemoteSearchResult() {
                Name = src.GetLocalName(),
                ImageUrl = src.Poster?.Url,
                PremiereDate = src.GetYearDate(),
                Overview = !string.IsNullOrWhiteSpace(src.Description) ? src.Description : src.ShortDescription,
                SearchProviderName = Constants.ProviderName
            };
            res.SetProviderId(Constants.ProviderId, src.Id.ToString(CultureInfo.InvariantCulture));

            return res;
        }

        public static IEnumerable<RemoteSearchResult> ToRemoteSearchResults(this SearchResponse<MovieDto> src, ILogger logger)
        {
            if (src?.Docs is null)
                return Enumerable.Empty<RemoteSearchResult>();

            return src.Docs
                .Select(s => s.ToRemoteSearchResult(logger))
                .Where(s => s != null);
        }

        private static RemoteSearchResult ToRemoteSearchResult(this MovieDto src, ILogger logger)
        {
            try
            {
                return src.ToRemoteSearchResult();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Exception during parse");
                return null;
            }
        }

        public static Series ToSeries(this MovieDto src, PremiereDateSource premiereDateSource = PremiereDateSource.World)
        {
            if (src is null)
                return null;

            var res = new Series();

            FillCommonFilmInfo(src, res, premiereDateSource);

            var endYear = src.GetEndYear();
            if (endYear.HasValue)
                res.EndDate = new DateTime(endYear.Value, 12, 31);

            res.Status = endYear.HasValue || string.Equals(src.Status, "completed", StringComparison.OrdinalIgnoreCase)
                ? SeriesStatus.Ended
                : SeriesStatus.Continuing;

            return res;
        }

        public static Movie ToMovie(this MovieDto src, PremiereDateSource premiereDateSource = PremiereDateSource.World)
        {
            if (src is null)
                return null;

            var res = new Movie();

            FillCommonFilmInfo(src, res, premiereDateSource);

            return res;
        }

        private static void FillCommonFilmInfo(MovieDto src, BaseItem dst, PremiereDateSource premiereDateSource)
        {
            dst.SetProviderId(Constants.ProviderId, src.Id.ToString(CultureInfo.InvariantCulture));
            dst.Name = src.GetLocalName();
            dst.OriginalTitle = src.GetOriginalNameIfNotSame();
            dst.PremiereDate = src.GetPremiereDate(premiereDateSource);
            if (src.Year > 1900)
                dst.ProductionYear = src.Year;
            if (!string.IsNullOrWhiteSpace(src.Slogan))
                dst.Tagline = src.Slogan;
            dst.Overview = !string.IsNullOrWhiteSpace(src.Description)
                ? src.Description
                : src.ShortDescription;
            if (src.Countries != null)
                dst.ProductionLocations = src.Countries
                    .Select(c => c?.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToArray();
            if (src.Genres != null)
                foreach (var genre in src.Genres.Select(c => NormalizeGenre(c?.Name)))
                    if (!string.IsNullOrWhiteSpace(genre))
                        dst.AddGenre(genre);

            dst.OfficialRating = NormalizeOfficialRating(src.AgeRating, src.RatingMpaa);

            // The length of a film, or of an episode for a series.
            var length = src.MovieLength > 0 ? src.MovieLength : src.SeriesLength;
            if (length > 0)
                dst.RunTimeTicks = TimeSpan.FromMinutes(length.Value).Ticks;

            dst.CommunityRating = src.GetCommunityRating();
            dst.CriticRating = src.GetCriticRating();

            if (!string.IsNullOrWhiteSpace(src.ExternalId?.Imdb))
                dst.SetProviderId(MetadataProvider.Imdb, src.ExternalId.Imdb);
        }

        /// <summary>
        /// Jellyfin's CommunityRating is a 0-10 score with one decimal: the Kinopoisk rating, or the IMDb
        /// rating when there is none. The API returns three decimals (7.416).
        /// </summary>
        public static float? GetCommunityRating(this MovieDto src)
        {
            var value = src?.Rating?.Kp ?? 0.0;
            if (value < 0.1)
                value = src?.Rating?.Imdb ?? 0.0;
            if (value < 0.1)
                return null;

            return (float)Math.Round(Math.Min(value, 10.0), 1, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Jellyfin's CriticRating is a whole 0-100 score (the web client shows it as a percentage).
        /// The API returns fractions (85.7143), they are rounded.
        /// </summary>
        public static float? GetCriticRating(this MovieDto src)
        {
            var rating = src?.Rating;
            if (rating is null)
                return null;

            // russianFilmCritics is already a percentage of positive reviews: the API returns 60
            // for the "60%" shown on the Kinopoisk page.
            if (rating.RussianFilmCritics > 0.0)
                return RoundPercentage(rating.RussianFilmCritics.Value);

            // filmCritics is a 0-10 score. A value above 10 can only be a
            // percentage, so it is kept as is instead of being multiplied by ten again.
            if (rating.FilmCritics > 0.0)
            {
                var value = rating.FilmCritics.Value <= 10.0
                    ? rating.FilmCritics.Value * 10.0
                    : rating.FilmCritics.Value;
                return RoundPercentage(value);
            }

            return null;
        }

        private static float RoundPercentage(double value)
            => (float)Math.Round(Math.Min(value, 100.0), 0, MidpointRounding.AwayFromZero);

        public static string NormalizeOfficialRating(int? ageLimit, string mpaa)
        {
            if (ageLimit is 0 or 6 or 12 or 16 or 18)
                return ageLimit.Value.ToString(CultureInfo.InvariantCulture) + "+";

            if (string.IsNullOrWhiteSpace(mpaa))
                return null;

            var normalizedMpaa = mpaa.Trim().ToUpperInvariant();
            return normalizedMpaa switch
            {
                "PG13" => "PG-13",
                "PG-13" => "PG-13",
                "R" => "R",
                "G" => "G",
                "PG" => "PG",
                "NC17" => "NC-17",
                "NC-17" => "NC-17",
                _ => normalizedMpaa
            };
        }

        private static string NormalizeGenre(string genre)
        {
            if (string.IsNullOrWhiteSpace(genre))
                return genre;

            var trimmed = genre.Trim();
            return char.ToUpper(trimmed[0], CultureInfo.InvariantCulture) + trimmed.Substring(1);
        }

        public static IEnumerable<RemoteImageInfo> ToRemoteImageInfos(this MovieDto src)
        {
            if (src is null || string.IsNullOrWhiteSpace(src.Poster?.Url))
                return Enumerable.Empty<RemoteImageInfo>();

            return new[]
            {
                new RemoteImageInfo
                {
                    Type = ImageType.Primary,
                    Url = src.Poster.Url,
                    Language = Constants.ProviderMetadataLanguage,
                    ProviderName = Constants.ProviderName
                }
            };
        }

        public static RemoteImageInfo ToRemoteImageInfo(this PersonDto src)
        {
            if (src is null || string.IsNullOrEmpty(src.Photo))
                return null;

            return new RemoteImageInfo(){
                Type = ImageType.Primary,
                Url = src.Photo,
                ProviderName = Constants.ProviderName
            };
        }

        public static PersonInfo ToPersonInfo(this PersonInMovieDto src)
        {
            if (src is null)
                return null;

            var res = new PersonInfo()
            {
                Name = src.Name,
                ImageUrl = src.Photo,
                Role = !string.IsNullOrWhiteSpace(src.Description) ? src.Description : Capitalize(src.Profession),
                Type = src.EnProfession.ToPersonType()
            };
            if (string.IsNullOrWhiteSpace(res.Name))
                res.Name = src.EnName ?? string.Empty;

            res.SetProviderId(Constants.ProviderId, src.Id.ToString(CultureInfo.InvariantCulture));

            return res;
        }

        /// <summary>
        /// The cast and crew of a title. Dubbing actors are left out: they are listed in addition to
        /// the actors themselves and would double the cast.
        /// </summary>
        public static IEnumerable<PersonInfo> ToPersonInfos(this IEnumerable<PersonInMovieDto> src)
        {
            if (src is null)
                return Enumerable.Empty<PersonInfo>();

            var res = src
                .Where(s => s != null && !string.Equals(s.EnProfession, "voiceover", StringComparison.OrdinalIgnoreCase))
                .Select(s => s.ToPersonInfo())
                .Where(s => s != null && !string.IsNullOrWhiteSpace(s.Name))
                .ToArray();

            var i = 0;
            foreach(var item in res)
                item.SortOrder = ++i;

            return res;
        }

        public static PersonKind ToPersonType(this string profession)
        {
            return profession?.Trim().ToLowerInvariant() switch
            {
                "actor" => PersonKind.Actor,
                "director" => PersonKind.Director,
                "writer" => PersonKind.Writer,
                "composer" => PersonKind.Composer,
                "producer" => PersonKind.Producer,
                "editor" => PersonKind.Editor,
                "translator" => PersonKind.Translator,
                _ => PersonKind.Unknown,
            };
        }

        private static string Capitalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var trimmed = value.Trim();
            return char.ToUpper(trimmed[0], CultureInfo.InvariantCulture) + trimmed.Substring(1);
        }

        public static DateTime? GetPremiereDate(this MovieDto src, PremiereDateSource source)
            => PremiereDateSelector.Select(src, source);

        /// <summary>January 1 of the production year.</summary>
        public static DateTime? GetYearDate(this MovieDto src)
            => src?.Year > 1900 ? new DateTime(src.Year.Value, 1, 1) : (DateTime?)null;

        /// <summary>
        /// The year a series finished, or <c>null</c> while it is still running or the year is unknown.
        /// </summary>
        public static int? GetEndYear(this MovieDto src)
        {
            var years = src?.ReleaseYears?.Where(year => year != null).ToList();
            if (years is null || years.Count == 0)
                return null;

            // An open range (the end is 0) means that the series is still running.
            if (years.Any(year => (year.End ?? 0) <= 1900))
                return null;

            return years.Max(year => year.End);
        }

        public static string GetLocalName(this MovieDto src)
        {
            var res = src?.Name;
            if (string.IsNullOrWhiteSpace(res))
                res = src?.AlternativeName;
            if (string.IsNullOrWhiteSpace(res))
                res = src?.EnName;
            return res;
        }

        public static string GetOriginalName(this MovieDto src)
            => !string.IsNullOrWhiteSpace(src?.AlternativeName)
                ? src.AlternativeName
                : (src.IsRussianSpokenOriginated()
                    ? src?.Name
                    : src?.EnName);

        public static string GetOriginalNameIfNotSame(this MovieDto src)
        {
            var localName = src.GetLocalName();
            var originalName = src.GetOriginalName();
            if (!string.IsNullOrWhiteSpace(originalName) && !string.Equals(localName, originalName))
                return originalName;

            return string.Empty;
        }

        public static bool IsRussianSpokenOriginated(this MovieDto src)
            => src?.Countries?.IsRussianSpokenOriginated() ?? false;

        public static bool IsRussianSpokenOriginated(this IEnumerable<NamedItemDto> src)
            => src?.Any(country => country?.Name == "Россия") ?? false;

        public static Person ToPerson(this PersonDto src)
        {
            if (src is null)
                return null;

            var res = new Person()
            {
                Name = src.GetLocalName(),
                PremiereDate = src.Birthday?.Date,
                EndDate = src.Death?.Date
            };

            var birthplace = string.Join(", ", (src.BirthPlace ?? new List<ValueDto>())
                .Select(place => place?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            if (birthplace.Length > 0)
                res.ProductionLocations = new[] { birthplace };

            return res;
        }

        public static string GetLocalName(this PersonDto src)
        {
            var res = src?.Name;
            if (string.IsNullOrWhiteSpace(res))
                res = src?.EnName;
            return res;
        }
    }
}
