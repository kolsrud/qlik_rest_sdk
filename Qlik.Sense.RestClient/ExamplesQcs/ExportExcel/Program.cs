using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json.Linq;
using Qlik.Sense.RestClient;

namespace ExportExcel
{
    class Program
    {
        static void Main(string[] args)
        {
            var url = "<url>";
            var apiKey = "<apiKey>";
            var appId = "<appId>";
            var objectId = "<objectId>";

            var outputFile = "output.xlsx";

            var client = new RestClientQcs(url);
            client.AsApiKey(apiKey);

            var requestBody = CreateRequestBody(appId, objectId);
            var httpRsp = client.PostHttpAsync("/api/v1/reports", requestBody);
            var httpResult = httpRsp.Result;
            var statusLocation = httpResult.Headers.Location;

            Console.WriteLine("Report generation requested. Awaiting process to complete...");
            var dataLocation = AwaitExportCompletion(client, statusLocation.AbsolutePath);

            Console.Write("Report generation completed. Downloading exported file... ");
            var dataLocationUri = new Uri(dataLocation);
            var bytes = client.GetBytes(dataLocationUri.AbsolutePath);
            Console.WriteLine("Done!");

            File.WriteAllBytes(outputFile, bytes);
            Console.WriteLine($"Wrote {bytes.Length} to file: {outputFile}");
        }

        private static string AwaitExportCompletion(RestClientQcs client, string statusLocation)
        {
            var rsp = client.Get<JToken>(statusLocation);
            while (rsp["status"].Value<string>() != "done")
            {
                Console.WriteLine("    Current status: " + rsp["status"]);
                Thread.Sleep(TimeSpan.FromSeconds(1));
                rsp = client.Get<JToken>(statusLocation);
            }
            Console.WriteLine("    Current status: " + rsp["status"]);

            return rsp["results"][0]["location"].Value<string>();
        }

        private static JToken CreateRequestBody(string appId, string objectId)
        {
            var body = new
            {
                type = "sense-data-1.0",
                output = new
                {
                    outputId = "Chart_excel",
                    type = "xlsx"
                },
                senseDataTemplate = new
                {
                    appId = appId,
                    id = objectId
                }
            };
            return JToken.FromObject(body);
        }
    }
}
