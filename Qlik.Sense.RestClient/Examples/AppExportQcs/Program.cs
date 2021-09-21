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
            // var appId = "<appId>";
            // var url = "<url>";
            // var apiKey = "<apiKey>";

            var appId = "0db23c0a-e67f-4bea-9cf3-0fc4498b0252";
            // var appId = "0db23c0a-e67f-4bea-9cf3-0fc4498b0253";
            var url = "https://yko.eu.qlikcloud.com/";
            var apiKey = "eyJhbGciOiJFUzM4NCIsImtpZCI6IjQ5NjYyMzMxLTViZDEtNGMwNi1iNDgwLTUwYTZlYTY5MDhkYiIsInR5cCI6IkpXVCJ9.eyJzdWJUeXBlIjoidXNlciIsInRlbmFudElkIjoiYWNaNGdFUE1nd3lOZm1lV0lpeElpWTNCY29FejllZjciLCJqdGkiOiI0OTY2MjMzMS01YmQxLTRjMDYtYjQ4MC01MGE2ZWE2OTA4ZGIiLCJhdWQiOiJxbGlrLmFwaSIsImlzcyI6InFsaWsuYXBpL2FwaS1rZXlzIiwic3ViIjoicmxuZTlYT2pTWkVQV2h3M241NF9BUkxzTWFqZjFoOFIifQ.fQroaAhGeYxB73OV0QfCeEA44aLfPMq_0O-X7dHk9BIw0_XNV5inZ0kAXkaQB005QYKl0dDaLx21PlpKj0aNAkIdtAJZRZPEWSF37-QE972a0S4Lo7poo3uJx3D26Q_U";

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
