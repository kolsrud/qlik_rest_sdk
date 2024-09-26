using System;
using Newtonsoft.Json.Linq;
using Qlik.Sense.RestClient;

namespace QcsConnectApiKey
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var url = "<url>";
            var apiKey = "<apiKey>";

            var restClient = new RestClient(url);
            restClient.AsApiKeyViaQcs(apiKey);

            Console.WriteLine(restClient.Get<JToken>("/api/v1/users/me"));
        }
    }
}
