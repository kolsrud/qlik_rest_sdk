using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Qlik.Sense.RestClient;

namespace AppExport
{
    class Program
    {
        static void Main(string[] args)
        {
            var url = "<url>";
            var appId = "<appId>";

            var restClient = new RestClient(url);
            restClient.AsNtlmUserViaProxy(false);

            Console.Write("Exporting... ");
            var rsp = restClient.Post<JObject>($"/qrs/app/{appId}/export/{Guid.NewGuid()}");

            var downloadPath = rsp["downloadPath"].ToString();
            var appName = downloadPath.Split('?').First().Split('/').Last();
            Console.WriteLine("Done.");
            Console.Write("Downloading... ");

            var stream = restClient.GetStream(downloadPath);
            using (var fileStream = File.OpenWrite(appName))
            {
                stream.CopyTo(fileStream);
            }

            Console.WriteLine("Done.");
            Console.WriteLine($"App export complete. Output file: {appName} ({new FileInfo(appName).Length} bytes)");
        }
    }
}
