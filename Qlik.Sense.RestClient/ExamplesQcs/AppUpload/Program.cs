using System;
using System.IO;
using Newtonsoft.Json.Linq;
using Qlik.Sense.RestClient;

namespace AppUpload
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var url = "<url>";
            var apiKey = "<apiKey>";
            var pathToApp = @"\path\to\app.qvf";

            var client = new RestClientQcs(url);
            client.AsApiKey(apiKey);

            var appFile = File.ReadAllBytes(pathToApp);
            var rsp = client.Post<JToken>("/api/v1/apps/import", appFile);
            Console.WriteLine(rsp);
        }
    }
}
