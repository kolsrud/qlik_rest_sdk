using System;
using System.Linq;
using Qlik.Sense.RestClient;

namespace BasicConnection
{
    class Program
    {
        static void Main(string[] args)
        {
            var senseServerUrl = args.Any() ? args[0] : "https://my.server.url";
            var restClient = new RestClient(senseServerUrl);
            restClient.AsNtlmUserViaProxy();
            Console.WriteLine(restClient.Get("/qrs/about"));
        }
    }
}
