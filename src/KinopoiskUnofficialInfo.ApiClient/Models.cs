using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace KinopoiskUnofficialInfo.ApiClient
{
    // Only the fields used by the plugin are described here. The rest of the API response is ignored.
    // Every field may be absent or null, so everything is nullable.

    public sealed class ImageDto
    {
        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("previewUrl")]
        public string PreviewUrl { get; set; }
    }

    public sealed class ExternalIdDto
    {
        [JsonProperty("imdb")]
        public string Imdb { get; set; }

        [JsonProperty("tmdb")]
        public int? Tmdb { get; set; }

        [JsonProperty("kpHD")]
        public string KpHd { get; set; }
    }

    public sealed class RatingDto
    {
        [JsonProperty("kp")]
        public double? Kp { get; set; }

        [JsonProperty("imdb")]
        public double? Imdb { get; set; }

        [JsonProperty("tmdb")]
        public double? Tmdb { get; set; }

        /// <summary>Critics score on a 0-10 scale.</summary>
        [JsonProperty("filmCritics")]
        public double? FilmCritics { get; set; }

        /// <summary>Share of positive reviews of Russian critics, 0-100.</summary>
        [JsonProperty("russianFilmCritics")]
        public double? RussianFilmCritics { get; set; }
    }

    /// <summary>A genre or a country.</summary>
    public sealed class NamedItemDto
    {
        [JsonProperty("id")]
        public int? Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; }
    }

    public sealed class ReleaseYearDto
    {
        [JsonProperty("start")]
        public int? Start { get; set; }

        /// <summary>0 or null while the series is still running.</summary>
        [JsonProperty("end")]
        public int? End { get; set; }
    }

    public sealed class PremiereDto
    {
        [JsonProperty("world")]
        public DateTime? World { get; set; }

        [JsonProperty("russia")]
        public DateTime? Russia { get; set; }

        [JsonProperty("usa")]
        public DateTime? Usa { get; set; }

        [JsonProperty("cinema")]
        public DateTime? Cinema { get; set; }

        [JsonProperty("digital")]
        public DateTime? Digital { get; set; }

        [JsonProperty("bluray")]
        public DateTime? Bluray { get; set; }

        [JsonProperty("dvd")]
        public DateTime? Dvd { get; set; }

        [JsonProperty("rerelease")]
        public DateTime? Rerelease { get; set; }
    }

    public sealed class VideoDto
    {
        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("site")]
        public string Site { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }

    public sealed class VideosDto
    {
        [JsonProperty("trailers")]
        public List<VideoDto> Trailers { get; set; }
    }

    public sealed class SeasonInfoDto
    {
        [JsonProperty("number")]
        public int? Number { get; set; }

        [JsonProperty("episodesCount")]
        public int? EpisodesCount { get; set; }
    }

    /// <summary>A cast or crew member listed in a movie.</summary>
    public sealed class PersonInMovieDto
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("enName")]
        public string EnName { get; set; }

        [JsonProperty("photo")]
        public string Photo { get; set; }

        /// <summary>The role (character), when the API provides it.</summary>
        [JsonProperty("description")]
        public string Description { get; set; }

        /// <summary>Profession in Russian, plural: "актеры", "режиссеры"...</summary>
        [JsonProperty("profession")]
        public string Profession { get; set; }

        /// <summary>Profession key: actor, director, writer, producer, composer, editor, operator, design, voiceover...</summary>
        [JsonProperty("enProfession")]
        public string EnProfession { get; set; }

        [JsonProperty("professionId")]
        public int? ProfessionId { get; set; }
    }

    /// <summary>
    /// A movie, series or other title. Used both for the full card (GET /v1.5/movie/{id})
    /// and for search and list results, which contain a subset of the fields.
    /// </summary>
    public sealed class MovieDto
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        /// <summary>Russian title.</summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>Original title.</summary>
        [JsonProperty("alternativeName")]
        public string AlternativeName { get; set; }

        [JsonProperty("enName")]
        public string EnName { get; set; }

        /// <summary>movie, tv-series, cartoon, anime, animated-series, tv-show...</summary>
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("isSeries")]
        public bool? IsSeries { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("year")]
        public int? Year { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("shortDescription")]
        public string ShortDescription { get; set; }

        [JsonProperty("slogan")]
        public string Slogan { get; set; }

        [JsonProperty("rating")]
        public RatingDto Rating { get; set; }

        /// <summary>Length of a movie, minutes.</summary>
        [JsonProperty("movieLength")]
        public int? MovieLength { get; set; }

        /// <summary>Length of an episode, minutes.</summary>
        [JsonProperty("seriesLength")]
        public int? SeriesLength { get; set; }

        [JsonProperty("ratingMpaa")]
        public string RatingMpaa { get; set; }

        /// <summary>Age limit: 0, 6, 12, 16 or 18.</summary>
        [JsonProperty("ageRating")]
        public int? AgeRating { get; set; }

        [JsonProperty("poster")]
        public ImageDto Poster { get; set; }

        [JsonProperty("backdrop")]
        public ImageDto Backdrop { get; set; }

        [JsonProperty("externalId")]
        public ExternalIdDto ExternalId { get; set; }

        [JsonProperty("genres")]
        public List<NamedItemDto> Genres { get; set; }

        [JsonProperty("countries")]
        public List<NamedItemDto> Countries { get; set; }

        [JsonProperty("persons")]
        public List<PersonInMovieDto> Persons { get; set; }

        [JsonProperty("premiere")]
        public PremiereDto Premiere { get; set; }

        [JsonProperty("releaseYears")]
        public List<ReleaseYearDto> ReleaseYears { get; set; }

        [JsonProperty("seasonsInfo")]
        public List<SeasonInfoDto> SeasonsInfo { get; set; }

        [JsonProperty("videos")]
        public VideosDto Videos { get; set; }
    }

    public sealed class EpisodeDto
    {
        [JsonProperty("number")]
        public int? Number { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("enName")]
        public string EnName { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("enDescription")]
        public string EnDescription { get; set; }

        [JsonProperty("airDate")]
        public DateTime? AirDate { get; set; }
    }

    public sealed class SeasonDto
    {
        [JsonProperty("movieId")]
        public int? MovieId { get; set; }

        [JsonProperty("number")]
        public int? Number { get; set; }

        [JsonProperty("episodesCount")]
        public int? EpisodesCount { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("airDate")]
        public DateTime? AirDate { get; set; }

        [JsonProperty("episodes")]
        public List<EpisodeDto> Episodes { get; set; }
    }

    public sealed class ValueDto
    {
        [JsonProperty("value")]
        public string Value { get; set; }
    }

    public sealed class PersonDto
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("enName")]
        public string EnName { get; set; }

        [JsonProperty("photo")]
        public string Photo { get; set; }

        [JsonProperty("birthday")]
        public DateTime? Birthday { get; set; }

        [JsonProperty("death")]
        public DateTime? Death { get; set; }

        /// <summary>Birth place as a list of parts, e.g. "Москва", "Россия".</summary>
        [JsonProperty("birthPlace")]
        public List<ValueDto> BirthPlace { get; set; }
    }

    /// <summary>Page-numbered response (movie search, person search).</summary>
    public sealed class SearchResponse<T>
    {
        [JsonProperty("docs")]
        public List<T> Docs { get; set; } = new List<T>();

        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("limit")]
        public int Limit { get; set; }

        [JsonProperty("page")]
        public int Page { get; set; }

        [JsonProperty("pages")]
        public int Pages { get; set; }
    }

    /// <summary>Cursor-paginated response (lists, seasons...).</summary>
    public sealed class CursorResponse<T>
    {
        [JsonProperty("docs")]
        public List<T> Docs { get; set; } = new List<T>();

        [JsonProperty("limit")]
        public int Limit { get; set; }

        [JsonProperty("next")]
        public string Next { get; set; }

        [JsonProperty("hasNext")]
        public bool HasNext { get; set; }
    }

    /// <summary>Token usage, GET /v1.5/token. This request is not counted against the limit.</summary>
    public sealed class TokenInfoDto
    {
        [JsonProperty("requestsLimit")]
        public int RequestsLimit { get; set; }

        [JsonProperty("requestsUsed")]
        public int RequestsUsed { get; set; }

        [JsonProperty("requestsRemaining")]
        public int RequestsRemaining { get; set; }

        /// <summary>Seconds until the counter is reset.</summary>
        [JsonProperty("ttl")]
        public int Ttl { get; set; }

        [JsonProperty("resetAt")]
        public DateTime? ResetAt { get; set; }
    }
}
