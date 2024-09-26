using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Qlik.Sense.RestClient;

namespace ListAllApps
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var url = "<url>";
            var apiKey = "<apiKey>";

            var restClient = new RestClient(url);
            restClient.AsApiKeyViaQcs(apiKey);

            // Fetch app items using page size 20 (maximum is 100).
            var allApps = FetchAll(restClient, "/api/v1/items?resourceType=app&limit=20").ToArray();
            Console.WriteLine($"Fetched {allApps.Length} apps.");
            foreach (var app in allApps)
            {
                Console.WriteLine($"  - {app["id"]} : \"{app["name"]}\"");
            }
        }

        public static IEnumerable<JObject> FetchAll(IRestClient restClient, string endpoint)
        {
            var data = new List<JObject>();
            var next = endpoint;
            // Continue fetching data as long as there is a "next" reference.
            while (next != null)
            {
                var spacesData = restClient.Get<JObject>(next);
                data.AddRange(spacesData["data"].Values<JObject>());
                next = spacesData["links"]["next"]?["href"]?.Value<string>()?.Substring(restClient.Url.Length);
            }

            return data;
        }
    }
}
