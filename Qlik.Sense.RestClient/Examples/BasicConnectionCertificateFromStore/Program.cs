using System;
using Newtonsoft.Json.Linq;
using Qlik.Sense.RestClient;

namespace BasicConnectionCertificateFromStore
{
    class Program
    {
        static void Main(string[] args)
        {
            var url = "<url>";
            var restClient = new RestClient(url);
            var certs = RestClient.LoadCertificateFromStore();
            restClient.AsDirectConnection(4242, false, certs);
            Console.WriteLine(restClient.Get<JToken>("/qrs/about"));
        }
    }
}
