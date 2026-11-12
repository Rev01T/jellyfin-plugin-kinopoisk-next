using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace KinopoiskUnofficialInfo.ApiClient
{
    public class DeclarationPatcherContractResolver : DefaultContractResolver
    {
        private readonly ICollection<JsonConverter> _badConverters = new JsonConverter[] {
            new BadIntegerConverter(),
            new BadDoubleConverter(),
            new BadProfessionKeyConverter(),
            new BadSpouseSexConverter(),
            new BadProductionStatusConverter(),
            new BadFilmSearchResponse_filmsTypeConverter(),
            new BadDistributionTypeConverter(),
            new BadDistributionSubTypeConverter(),
        };

        protected override IList<JsonProperty> CreateProperties(System.Type type, MemberSerialization memberSerialization)
        {
            var list = base.CreateProperties(type, memberSerialization);
            foreach (var property in list)
            {
                foreach (var badConverter in _badConverters)
                {
                    if (badConverter.CanConvert(property.PropertyType))
                    {
#pragma warning disable CS0618
                        property.MemberConverter = null;
#pragma warning restore CS0618
                        property.Converter = badConverter;
                        break;
                    }
                }

                // openapi.json marks most properties as required, but the API returns null
                // (for example distribution country) or omits them. Strictness gives the
                // plugin nothing, while a single null would discard the whole response.
                property.Required = Required.Default;
            }
            return list;
        }
    }
}