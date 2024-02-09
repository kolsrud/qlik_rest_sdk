using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Qlik.Sense.RestClient;

namespace PublishApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var senseServerUrl = args.Any() ? args[0] : "https://my.server.url";
            var restClient = new RestClient(senseServerUrl);
            restClient.AsNtlmUserViaProxy();
            var appId = "app-identifer";
            var streamId = "stream-identifier";
            using (new RestClientDebugConsole())
                restClient.Put($"/qrs/app/{appId}/publish?stream={streamId}");
        }
    }
}