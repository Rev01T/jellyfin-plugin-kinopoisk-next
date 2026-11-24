using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace KinopoiskUnofficialInfo.ApiClient
{
    /// <summary>
    /// Reads API responses. A field that cannot be read (an unexpected type, a broken date)
    /// is skipped and logged once, instead of failing the whole response and losing the
    /// rest of the metadata.
    /// </summary>
    internal sealed class ApiJson
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, byte> _reported = new ConcurrentDictionary<string, byte>();

        public ApiJson(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public T Deserialize<T>(string json, string endpoint) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var settings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                Error = (sender, args) =>
                {
                    var key = endpoint + ":" + args.ErrorContext.Path;
                    if (_reported.TryAdd(key, 0))
                        _logger.LogWarning(
                            "Skipping unreadable field {Path} in the response of {Endpoint}: {Error}",
                            args.ErrorContext.Path,
                            endpoint,
                            args.ErrorContext.Error.Message);
                    args.ErrorContext.Handled = true;
                }
            };

            return JsonConvert.DeserializeObject<T>(json, settings);
        }
    }
}
