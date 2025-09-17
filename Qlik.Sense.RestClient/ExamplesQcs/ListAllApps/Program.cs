using System;
using Qlik.Sense.RestClient;

namespace ListAllApps
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var url = "<url>";
            var apiKey = "<apiKey>";
            
            var restClient = new RestClientQcs(url);
            restClient.AsApiKey(apiKey);

            Console.WriteLine("Fetching all apps:");
            var appCnt = 0;
            // Fetch app items using page size 20 (maximum is 100).
            var allApps = restClient.FetchAllItems("app", 20);
            foreach (var app in allApps)
            {
                appCnt++;
                Console.WriteLine($"  - {app["id"]} : \"{app["name"]}\"");
            }
			Console.WriteLine($"Fetched {appCnt} apps.");
		}
    }
}
