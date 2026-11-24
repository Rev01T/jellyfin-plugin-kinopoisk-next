using System;

namespace KinopoiskUnofficialInfo.ApiClient
{
    /// <summary>
    /// A failed request to the poiskkino.dev API.
    /// </summary>
    public class ApiException : Exception
    {
        public ApiException(string message, int statusCode, string response, bool isQuotaExceeded = false, Exception innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
            Response = response ?? string.Empty;
            IsQuotaExceeded = isQuotaExceeded;
        }

        /// <summary>HTTP status code of the response (402 for the client's own "paused until reset" error).</summary>
        public int StatusCode { get; }

        /// <summary>Raw response body, if any.</summary>
        public string Response { get; }

        /// <summary>
        /// True when the daily request limit of the token is exhausted. Such requests are not
        /// retried until the quota is reset.
        /// </summary>
        public bool IsQuotaExceeded { get; }
    }
}
