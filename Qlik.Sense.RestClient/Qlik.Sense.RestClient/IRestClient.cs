using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;

using Qlik.Sense.RestClient.Qrs;

namespace Qlik.Sense.RestClient
{
    public interface IRestClient : IRestClientGeneric
    {
        User User { get; }
        string UserId { get; }
        string UserDirectory { get; }
        [Obsolete("Use class IRestClientQcs to interact with QCS.")] // Obsolete since October 2024, v2.0.0
        QcsSessionInfo QcsSessionInfo { get; }

        void AsDirectConnection(int port = 4242, bool certificateValidation = true, X509Certificate2Collection certificateCollection = null);
        void AsDirectConnection(string userDirectory, string userId, int port = 4242, bool certificateValidation = true, X509Certificate2Collection certificateCollection = null);
        void AsJwtViaProxy(string key, bool certificateValidation = true);
        void AsNtlmUserViaProxy(bool certificateValidation = true);
        void AsNtlmUserViaProxy(NetworkCredential credential, bool certificateValidation = true);
        void AsAnonymousUserViaProxy(bool certificateValidation = true);
        void AsStaticHeaderUserViaProxy(string userId, string headerName, bool certificateValidation = true);
        void AsExistingSessionViaProxy(string sessionId, string cookieHeaderName, bool proxyUsesSsl = true, bool certificateValidation = true);

        Cookie GetCookie(string name);
        CookieCollection GetCookies();
        
        IRestClient ConnectAsQmc();
        IRestClient ConnectAsHub();
        IRestClient WithXrfkey(string xrfkey);
        IRestClient WithContentType(string contentType);
    }
}
