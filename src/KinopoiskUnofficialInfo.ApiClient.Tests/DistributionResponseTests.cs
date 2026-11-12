using Newtonsoft.Json;
using Xunit;

namespace KinopoiskUnofficialInfo.ApiClient.Tests
{
    public class DistributionResponseTests
    {
        private static DistributionResponse Deserialize(string json)
            => JsonConvert.DeserializeObject<DistributionResponse>(
                json,
                new JsonSerializerSettings
                {
                    ContractResolver = new DeclarationPatcherContractResolver()
                });

        [Fact]
        public void ShouldDeserializeNullSubType()
        {
            const string json =
                "{\"total\":1,\"items\":[{\"type\":\"PREMIERE\",\"subType\":null," +
                "\"date\":\"2020-01-10\",\"reRelease\":false," +
                "\"country\":{\"country\":\"Россия\"},\"companies\":[]}]}";

            var result = Deserialize(json);

            var item = Assert.Single(result.Items);
            Assert.Equal(DistributionType.PREMIERE, item.Type);
            Assert.Equal((DistributionSubType)(-1), item.SubType);
            Assert.Equal("2020-01-10", item.Date);
        }

        [Fact]
        public void ShouldDeserializeNullCountryAndCompanies()
        {
            const string json =
                "{\"total\":1,\"items\":[{\"type\":\"WORLD_PREMIER\",\"subType\":null," +
                "\"date\":null,\"reRelease\":false,\"country\":null,\"companies\":null}]}";

            var result = Deserialize(json);

            var item = Assert.Single(result.Items);
            Assert.Equal(DistributionType.WORLD_PREMIER, item.Type);
            Assert.Null(item.Date);
            Assert.Null(item.Country);
        }

        [Fact]
        public void ShouldDeserializeUnknownEnumValues()
        {
            const string json =
                "{\"total\":1,\"items\":[{\"type\":\"SOMETHING_NEW\",\"subType\":\"STREAMING\"," +
                "\"date\":\"2020-01-10\",\"reRelease\":false," +
                "\"country\":{\"country\":\"США\"},\"companies\":[]}]}";

            var result = Deserialize(json);

            var item = Assert.Single(result.Items);
            Assert.Equal((DistributionType)(-1), item.Type);
            Assert.Equal((DistributionSubType)(-1), item.SubType);
        }

        [Fact]
        public void ShouldKeepKnownEnumValues()
        {
            const string json =
                "{\"total\":1,\"items\":[{\"type\":\"WORLD_PREMIER\",\"subType\":\"CINEMA\"," +
                "\"date\":\"2020-01-10\",\"reRelease\":false," +
                "\"country\":{\"country\":\"США\"},\"companies\":[{\"name\":\"X\"}]}]}";

            var result = Deserialize(json);

            var item = Assert.Single(result.Items);
            Assert.Equal(DistributionType.WORLD_PREMIER, item.Type);
            Assert.Equal(DistributionSubType.CINEMA, item.SubType);
            Assert.Equal("США", item.Country.Country1);
        }
    }
}