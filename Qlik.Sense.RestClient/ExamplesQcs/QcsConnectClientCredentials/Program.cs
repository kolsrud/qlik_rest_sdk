using System;
using Newtonsoft.Json.Linq;
using Qlik.Sense.RestClient;

namespace QcsConnectClientCredentials
{
    class Program
    {
        static void Main(string[] args)
        {
            var url = "<url>";
            var clientId = "<clientId>";
            var clientSecret = "<clientSecret>";

            var restClient = new RestClient(url);
            restClient.AsClientCredentialsViaQcs(clientId, clientSecret);

            Console.WriteLine(restClient.Get<JToken>("/api/v1/users/me"));
        }
    }
}
