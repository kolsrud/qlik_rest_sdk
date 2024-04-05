using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using Qlik.Sense.RestClient;

namespace CreateStream
{
    class Program
    {
        static void Main(string[] args)
        {
	        var url = "<url>";
            var restClient = new RestClient(url);
            restClient.AsNtlmUserViaProxy();

            dynamic body = new JObject();
            body.Name = "MyStream";
            Console.WriteLine(restClient.Post("qrs/stream", body.ToString()));
        }
    }
}
