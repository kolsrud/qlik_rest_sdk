using System;
using System.Linq;
using Newtonsoft.Json;

namespace Qlik.Sense.RestClient.Qrs
{
    public class User
    {
        [JsonProperty("userId")]
        public string Id { get; set; }

        [JsonProperty("userDirectory")]
        public string Directory { get; set; }

        public User() { }

        public User(string usr)
        {
            var sections = usr.Split('\\');
            if (sections.Length != 2 || sections.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException($"User must be of pattern \"<userDirectory>\\<userId>\". Actual value was \"{usr}\"", nameof(usr) );

            Directory = sections[0];
            Id = sections[1];
        }

        public override string ToString()
        {
            return Directory + "\\" + Id;
        }
    }
}