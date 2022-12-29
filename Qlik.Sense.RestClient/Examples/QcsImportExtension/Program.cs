using System;
using System.IO;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Qlik.Sense.RestClient;

namespace QcsImportExtension
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var url = "<url>";
            var apiKey = "<apiKey>";

            var restClient = new RestClient(url);
            restClient.AsApiKeyViaQcs(apiKey);

            var extensionPath = @"C:\Path\To\Extension.zip";
            var tags = new [] { "MyTag" }; // Set of tags. Optional

            var dataContent = new StringContent(JObject.FromObject(new { tags }).ToString(Formatting.None));
            var fileContent = new ByteArrayContent(File.ReadAllBytes(extensionPath));

            var content = new MultipartFormDataContent
            {
                { dataContent, "data" }, // Optional
                { fileContent, "file", Path.GetFileName(extensionPath) }
            };

            var rsp = restClient.Post<JToken>("/api/v1/extensions", content);
            Console.WriteLine(rsp.ToString());
        }
    }
}
