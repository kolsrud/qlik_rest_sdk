using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Qlik.Sense.RestClient;

namespace AppUpload
{
    class Program
    {
		static void Main(string[] args)
		{
			var url = "<url>";
			var restClient = new RestClient(url);
			restClient.AsNtlmUserViaProxy();

			const string filePath = @"\path\to\app.qvf";
			const string nameOfApp = "MyUploadedApp";

			// For large apps, a streaming approach is recommended.
			var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
			var streamContent = new StreamContent(fileStream);
			streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.qlik.sense.app");

			Console.WriteLine(restClient.PostHttp("/qrs/app/upload?keepData=true&name=" + nameOfApp, streamContent));
		}
	}
}
