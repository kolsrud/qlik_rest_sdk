using System;
using Newtonsoft.Json.Linq;
using Qlik.Sense.RestClient;

namespace ListAppsInAllStreams
{
    class Program
    {
        static void Main(string[] args)
        {
            var url = "<url>";
            var restClient = new RestClient(url);
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
