using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace KinopoiskUnofficialInfo.ApiClient
{
    /// <summary>
    /// The API returns null for distribution subType (for example for some premiere
    /// entries) although openapi.json declares it as a required enum.
    /// </summary>
    public class BadDistributionSubTypeConverter : StringEnumConverter
    {
        public override bool CanConvert(System.Type objectType)
            => objectType == typeof(DistributionSubType);

        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            try
            {
                return base.ReadJson(reader, objectType, existingValue, serializer);
            }
            catch (Exception)
            {
                return (DistributionSubType)(-1);
            }
        }
    }
}