using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace Qlik.Sense.RestClient
{
    internal class ConnectionSettings : IConnectionConfigurator
    {
        public Uri BaseUri { get; set; }

        public CookieContainer CookieJar { get; set; }

        private bool _isConfigured = false;
        public RestClient.ConnectionType ConnectionType;
        public string UserDirectory;
        public string UserId;
        public string StaticHeaderName;
        public bool UseDefaultCredentials;
        public X509Certificate2Collection Certificates;
        public Action<HttpWebRequest> WebRequestTransform { get; set; }

        public bool HasCookie => CookieJar.Count > 0;

        public ConnectionSettings Clone()
        {
            return new ConnectionSettings()
            {
                BaseUri = this.BaseUri,
                CookieJar = this.CookieJar,
                _isConfigured = this._isConfigured,
                ConnectionType = this.ConnectionType,
                UserDirectory = this.UserDirectory,
                UserId = this.UserId,
                StaticHeaderName = this.StaticHeaderName,
                UseDefaultCredentials = this.UseDefaultCredentials,
                Certificates = this.Certificates,
                WebRequestTransform = this.WebRequestTransform
            };
        }

        private ConnectionSettings()
        {
            CookieJar = new CookieContainer();
        }

        public ConnectionSettings(string uri) : this()
        {
            BaseUri = new Uri(uri);
        }

        public void AsDirectConnection(int port = 4242, bool certificateValidation = true,
            X509Certificate2Collection certificateCollection = null)
        {
            AsDirectConnection(Environment.UserName, Environment.UserDomainName, port, certificateValidation,
                certificateCollection);
        }

        public void AsDirectConnection(string userDirectory, string userId, int port = 4242,
            bool certificateValidation = true, X509Certificate2Collection certificateCollection = null)
        {
            ConnectionType = RestClient.ConnectionType.DirectConnection;
            var uriBuilder = new UriBuilder(BaseUri) {Port = port};
            BaseUri = uriBuilder.Uri;
            UserId = userId;
            UserDirectory = userDirectory;
            Certificates = certificateCollection;
            if (!certificateValidation)
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            _isConfigured = true;
        }

        public void AsNtlmUserViaProxy(bool certificateValidation = true)
        {
            UseDefaultCredentials = true;
            ConnectionType = RestClient.ConnectionType.NtlmUserViaProxy;
            UserId = Environment.UserName;
            UserDirectory = Environment.UserDomainName;
            if (!certificateValidation)
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            _isConfigured = true;
        }

        public void AsStaticHeaderUserViaProxy(string userId, string headerName, bool certificateValidation = true)
        {
            ConnectionType = RestClient.ConnectionType.StaticHeaderUserViaProxy;
            UserId = userId;
            UserDirectory = Environment.UserDomainName;
            StaticHeaderName = headerName;
            if (!certificateValidation)
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            _isConfigured = true;
        }

        public void Validate()
        {
            if (!_isConfigured)
                throw new RestClient.ConnectionNotConfiguredException();
            if (ConnectionType == RestClient.ConnectionType.DirectConnection && Certificates == null)
                throw new RestClient.CertificatesNotLoadedException();
        }
    }
}