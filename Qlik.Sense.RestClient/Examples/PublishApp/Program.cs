using System;
using Qlik.Sense.RestClient;

namespace PublishApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var url = "<url>";
            var restClient = new RestClient(url);
            restClient.AsNtlmUserViaProxy();
            var appId = "<appId>";
            var streamId = "<streamId>";
            Console.WriteLine(restClient.Put($"/qrs/app/{appId}/publish?stream={streamId}"));
        }
    }
}