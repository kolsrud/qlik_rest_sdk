using System;
using System.IO;
using System.Net;
using Qlik.Sense.RestClient;

namespace AppExportQcs
{
    class Program
    {
        static void Main(string[] args)
        {
            var appId = "<appId>";
            var url = "<url>";
            var apiKey = "<apiKey>";

            var outputFile = "myapp.qvf";

            var restClient = new RestClient(url);
            restClient.AsApiKeyViaQcs(apiKey);

            var rsp = restClient.PostHttpAsync($"/api/v1/apps/{appId}/export", throwOnFailure: false).Result;
            Console.WriteLine($"Request returned status code: {(int)rsp.StatusCode} ({rsp.StatusCode})");
            if (rsp.StatusCode == HttpStatusCode.Created)
            {
                var downloadPath = rsp.Headers.Location.OriginalString;
                Console.WriteLine("Download file location: " + downloadPath);

                Console.Write("Downloading... ");

                var stream = restClient.GetStream(downloadPath);
                using (var fileStream = File.OpenWrite(outputFile))
                {
                    stream.CopyTo(fileStream);
                }

                Console.WriteLine("Done.");
                Console.WriteLine($"App export complete. Output file: {outputFile} ({new FileInfo(outputFile).Length} bytes)");
            }
        }
    }
}
