using Newtonsoft.Json;

namespace Qlik.Sense.RestClient.Qrs
{
    public class User
    {
        [JsonProperty("userId")]
        public string Id { get; set; }

        [JsonProperty("userDirectory")]
        public string Directory { get; set; }

        public override string ToString()
        {
            return Directory + "\\" + Id;
        }
    }
}