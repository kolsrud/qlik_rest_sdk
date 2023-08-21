using System;
using System.Linq;
using Qlik.Sense.RestClient;

namespace BasicConnection
{
    class Program
    {
        static void Main(string[] args)
        {
            var senseServerUrl = args.Any() ? args[0] : "https://rd-yko-dnettest.rdlund.qliktech.com";
            var restClient = new RestClient(senseServerUrl);
            restClient.AsNtlmUserViaProxy(false);
            using (new RestClientDebugConsole())
                restClient.Get("/qrs/about");
        }
    }
}
