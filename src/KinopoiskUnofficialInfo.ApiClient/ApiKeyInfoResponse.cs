using Newtonsoft.Json;

namespace KinopoiskUnofficialInfo.ApiClient
{
    /// <summary>
    /// Usage information returned by GET /api/v1/api_keys/{apiKey}.
    /// </summary>
    public sealed class ApiKeyInfoResponse
    {
        [JsonProperty("accountType")]
        public string AccountType { get; set; }

        [JsonProperty("dailyQuota")]
        public ApiKeyQuota DailyQuota { get; set; }

        [JsonProperty("totalQuota")]
        public ApiKeyQuota TotalQuota { get; set; }
    }

    public sealed class ApiKeyQuota
    {
        [JsonProperty("value")]
        public int Value { get; set; }

        [JsonProperty("used")]
        public int Used { get; set; }
    }
}
