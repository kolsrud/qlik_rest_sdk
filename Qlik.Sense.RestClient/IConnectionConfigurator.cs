using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace Qlik.Sense.RestClient
{
    public interface IConnectionConfigurator
    {
        void AsDirectConnection(int port = 4242, X509Certificate2Collection certificateCollection = null);

        void AsDirectConnection(string userDirectory, string userId, int port = 4242, X509Certificate2Collection certificateCollection = null);

        void AsNtlmUserViaProxy(NetworkCredential credential);

        void AsNtlmUserViaProxy();

        void AsStaticHeaderUserViaProxy(string userId, string headerName);
    }
}