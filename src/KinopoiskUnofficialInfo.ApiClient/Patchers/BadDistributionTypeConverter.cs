using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace KinopoiskUnofficialInfo.ApiClient
{
    /// <summary>
    /// The API may return null or a value that is missing from the published
    /// openapi.json for the distribution type. Such values must not make the whole
    /// response undeserializable.
    /// </summary>
    public class BadDistributionTypeConverter : StringEnumConverter
    {
        public override bool CanConvert(System.Type objectType)
            => objectType == typeof(DistributionType);

        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            try
            {
                return base.ReadJson(reader, objectType, existingValue, serializer);
            }
            catch (Exception)
            {
                return (DistributionType)(-1);
            }
        }
    }
}