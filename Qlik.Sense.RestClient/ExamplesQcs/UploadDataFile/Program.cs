using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Qlik.Sense.RestClient;

namespace UploadDataFile
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var url = "<url>";
            var apiKey = "<apiKey>";
            var filePath = @"<path to file>";

            var fileName = Path.GetFileName(filePath);
            var restClient = new RestClientQcs(url);
            restClient.AsApiKey(apiKey);

            // ********************************************************
            // * Large files must first be uploaded to temp-contents
            // ********************************************************
            Console.WriteLine($"Uploading file \"{fileName}\" to temp-contents... ");
            var streamContent = new StreamContent(File.OpenRead(filePath));
            var rsp = restClient.PostHttp($"/api/v1/temp-contents?filename={fileName}", streamContent);
            var location = rsp.Headers.Location.ToString();
            Console.WriteLine("File created: " + location);

            // ********************************************************
            // * The created file can now be imported to data-files
            // ********************************************************
            Console.WriteLine("Importing temporary file to data-files... ");
            var json = JObject.FromObject(new
            {
                name = fileName,
                tempContentFileId = location.Split('/').Last()
            });
            var content = new MultipartFormDataContent
            {
                { new StringContent(json.ToString(Formatting.None)), "Json" },
            };

            restClient.Post("/api/v1/data-files", content);
            Console.WriteLine("Done!");
        }
    }
}