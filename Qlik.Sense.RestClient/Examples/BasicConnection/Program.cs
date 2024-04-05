using System;
using System.Linq;
using Qlik.Sense.RestClient;

namespace BasicConnection
{
    class Program
    {
        static void Main(string[] args)
        {
	        var url = "<url>";
            var restClient = new RestClient(url);
            restClient.AsNtlmUserViaProxy(false);
            using (new RestClientDebugConsole())
                restClient.Get("/qrs/about");
        }
    }
}
