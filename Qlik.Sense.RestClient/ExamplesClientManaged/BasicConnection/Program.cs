using System;
using Newtonsoft.Json.Linq;
using Qlik.Sense.RestClient;

namespace BasicConnection
{
    class Program
    {
        static void Main(string[] args)
        {
            var url = "<url>";
            var restClient = new RestClient(url);
            restClient.AsNtlmUserViaProxy(false);
            Console.WriteLine(restClient.Get<JToken>("/qrs/about"));
        }
    }
}
