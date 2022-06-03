using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using Qlik.Sense.RestClient;

namespace ListAppsInAllStreams
{
    class Program
    {
        static void Main(string[] args)
        {
            var senseServerUrl = args.Any() ? args[0] : "https://my.server.url";
            var restClient = new RestClient(senseServerUrl);
            restClient.AsNtlmUserViaProxy();
            foreach (var stream in restClient.Get<JArray>("/qrs/stream"))
            {
                Console.WriteLine($"Apps in stream: {stream["id"]}: {stream["name"]}");
                foreach (var app in restClient.Get<JArray>($"/qrs/app?filter=stream.id eq {stream["id"]}"))
                {
                    Console.WriteLine($"  {app["id"]}: {app["name"]}");
                }
            }
        }
    }
}
