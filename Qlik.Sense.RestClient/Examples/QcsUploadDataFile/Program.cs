using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Qlik.Sense.RestClient;

namespace QcsUploadDataFile
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var url = "https://yko.eu.qlikcloud.com/";
            var apiKey = "eyJhbGciOiJFUzM4NCIsImtpZCI6IjVlNzM4Yjg5LWZmMmQtNGFiNC1hN2QyLTdlOTgzNDI0OGUyMSIsInR5cCI6IkpXVCJ9.eyJzdWJUeXBlIjoidXNlciIsInRlbmFudElkIjoiYWNaNGdFUE1nd3lOZm1lV0lpeElpWTNCY29FejllZjciLCJqdGkiOiI1ZTczOGI4OS1mZjJkLTRhYjQtYTdkMi03ZTk4MzQyNDhlMjEiLCJhdWQiOiJxbGlrLmFwaSIsImlzcyI6InFsaWsuYXBpL2FwaS1rZXlzIiwic3ViIjoicmxuZTlYT2pTWkVQV2h3M241NF9BUkxzTWFqZjFoOFIifQ.wBazjYcMa2fdpReObpq_Hg6w2CgsDZ-1Ja8v6ZexnDr89GI-QQWwofG20r2V6JoZVSBLC7Hwfp1Ba4UrDBtSjnd86lseFxPnK5TV1Y6cewGOijp7-pf7UDho5CfE4sxY";
            var filename = "smallfile.dat";

            var restClient = new RestClient(url);
            restClient.AsApiKeyViaQcs(apiKey);

            // ********************************************************
            // * Large files must first be uploaded to temp-contents
            // ********************************************************
            Console.WriteLine($"Uploading file \"{filename}\" to temp-contents... ");
            var streamContent = new StreamContent(File.OpenRead($@"C:\Tmp\{filename}"));
            var rsp = restClient.PostHttpAsync($"/api/v1/temp-contents?filename={filename}", streamContent).Result;
            var location = rsp.Headers.Location.ToString();
            Console.WriteLine("File created: " + location);

            // ********************************************************
            // * The created file can now be imported to data-files
            // ********************************************************
            Console.WriteLine("Importing temporary file to data-files... ");
            var json = JObject.FromObject(new
            {
                name = filename,
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