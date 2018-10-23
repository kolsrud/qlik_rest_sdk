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
            using (new DebugConsole())
                restClient.Put(string.Format("/qrs/app/{0}/publish?stream={1}", appId, streamId), "");
        }
    }
}