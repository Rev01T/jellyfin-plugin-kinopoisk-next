using System.Collections.Generic;
using Newtonsoft.Json;

namespace KinopoiskUnofficialInfo.ApiClient
{
    public sealed class FilmImagesResponse
    {
        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("totalPages")]
        public int TotalPages { get; set; }

        [JsonProperty("items")]
        public List<FilmImage> Items { get; set; } = new List<FilmImage>();
    }

    public sealed class FilmImage
    {
        [JsonProperty("imageUrl")]
        public string ImageUrl { get; set; }

        [JsonProperty("previewUrl")]
        public string PreviewUrl { get; set; }
    }
}
