using System.Security.Authentication;
using System.Threading.Tasks;
using System;
using System.Net;

namespace Qlik.Sense.RestClient
{
    public interface IRestClientQcs : IRestClientGeneric
    {
        QcsSessionInfo QcsSessionInfo { get; }

        void AsApiKey(string apiKey);
        void AsJwt(string jwt);
        void AsExistingSessionViaQcs(QcsSessionInfo sessionInfo);

        IRestClientQcs WithContentType(string contentType);
    }

    public class RestClientQcs : RestClientGeneric, IRestClientQcs
    {
        private RestClientQcs(RestClientQcs source) : base(source)
        {
        }

        public RestClientQcs(Uri uri) : base(uri)
        {
            _connectionSettings.IsQcs = true;
        }

        public RestClientQcs(string uri) : this(new Uri(uri))
        {
        }

        public void AsApiKey(string apiKey)
        {
            _connectionType = ConnectionType.ApiKeyViaQcs;
            _connectionSettings.AllowAutoRedirect = false;
            AddBearerToken(apiKey);
            _connectionSettings.IsAuthenticated = true;
        }

        public void AsJwt(string jwt)
        {
            _connectionType = ConnectionType.JwtTokenViaQcs;
            AddBearerToken(jwt);
            _connectionSettings.IsAuthenticated = false;
            _connectionSettings.IsQcs = true;
            _connectionSettings.AuthenticationFunc = CollectCookieJwtViaQcsAsync;
        }

        public void AsExistingSessionViaQcs(QcsSessionInfo sessionInfo)
        {
            _connectionType = ConnectionType.ExistingSessionViaQcs;
            _connectionSettings.IsQcs = true;
            _connectionSettings.CookieJar.Add(BaseUri, new Cookie("eas.sid", sessionInfo.EasSid));
            _connectionSettings.CookieJar.Add(BaseUri, new Cookie("eas.sid.sig", sessionInfo.EasSidSig));
            CustomHeaders[SenseHttpClient.CSRF_TOKEN_ID] = sessionInfo.SessionToken;
        }

        public IRestClientQcs WithContentType(string contentType)
        {
            var client = new RestClientQcs(this);
            client._connectionSettings.ContentType = contentType;
            return client;
        }

        private void AddBearerToken(string token)
        {
            CustomHeaders.Add("Authorization", "Bearer " + token);
        }

        private async Task CollectCookieJwtViaQcsAsync()
        {
            RestClientDebugConsole?.Log($"Authenticating (calling POST /login/jwt-session)");
            var client = GetClient();
            await LogReceive(client.PostStringAsync(BaseUri.Append("/login/jwt-session"), "")).ConfigureAwait(false);

            var csrfToken = _connectionSettings.GetCookie("_csrfToken").Value;
            if (csrfToken == null)
            {
                throw new AuthenticationException("Call to /login/jwt-session did not return a csrf token cookie.");
            }

            client.AddDefaultHeader(SenseHttpClient.CSRF_TOKEN_ID, csrfToken);
            RestClientDebugConsole?.Log($"Authentication complete.");
        }
    }
}