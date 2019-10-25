using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Qlik.Sense.RestClient
{
    public enum ConnectionType
    {
        NtlmUserViaProxy,
        StaticHeaderUserViaProxy,
        DirectConnection
    }

    internal class ConnectionSettings : IConnectionConfigurator
    {
        public Uri BaseUri { get; set; }

        public CookieContainer CookieJar { get; set; }
        public bool IsAuthenticated { get; private set; }

        private bool _isConfigured = false;
        public ConnectionType ConnectionType;
        public string UserDirectory;
        public string UserId;
        public string StaticHeaderName;
        public ICredentials CustomCredential;
        public TimeSpan Timeout;
        public string Xrfkey;
        public Dictionary<string, string> CustomHeaders { get; private set; }= new Dictionary<string, string>();

        public bool CertificateValidation = true;
        public X509Certificate2Collection Certificates;
        // TODO: Remove this member?
        public Action<HttpClient> WebRequestTransform { get; set; }
        public string ContentType { get; set; } = "application/json";

        private Exception _authenticationException;

        public Func<Task> AuthenticationFunc { get; set; }

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public async Task PerformAuthentication()
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            if (IsAuthenticated)
            {
                _semaphore.Release();
                return;
            }

            if (_authenticationException != null)
            {
                _semaphore.Release();
                throw _authenticationException;
            }

            try
            {
                await AuthenticationFunc().ConfigureAwait(false);
                IsAuthenticated = true;
            }
            catch (Exception e)
            {
                _authenticationException = e;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public ConnectionSettings Clone()
        {
            return new ConnectionSettings()
            {
                BaseUri = this.BaseUri,
                CookieJar = this.CookieJar,
                IsAuthenticated = this.IsAuthenticated,
                _isConfigured = this._isConfigured,
                ConnectionType = this.ConnectionType,
                UserDirectory = this.UserDirectory,
                UserId = this.UserId,
                StaticHeaderName = this.StaticHeaderName,
                CertificateValidation = this.CertificateValidation,
                Certificates = this.Certificates,
                CustomCredential = this.CustomCredential,
                Timeout = this.Timeout,
                Xrfkey = this.Xrfkey,
                CustomHeaders = new Dictionary<string, string>(this.CustomHeaders),
                WebRequestTransform = this.WebRequestTransform,
                ContentType = this.ContentType,
                AuthenticationFunc = this.AuthenticationFunc
            };
        }

        private ConnectionSettings()
        {
        }

        public void SetXrfKey(string xrfkey)
        {
            if (xrfkey.Length != 16) throw new ArgumentException("Xrfkey must be of length 16.", nameof(xrfkey));
            var r = new Regex("^[a-zA-Z0-9]*$");
            if (!r.IsMatch(xrfkey)) throw new ArgumentException("Xrfkey contains illegal character.", nameof(xrfkey));
            Xrfkey = xrfkey;
        }

        public ConnectionSettings(string uri) : this()
        {
            Timeout = System.Threading.Timeout.InfiniteTimeSpan;
            CookieJar = new CookieContainer();
            BaseUri = new Uri(uri);
        }

        public void AsDirectConnection(int port = 4242, bool certificateValidation = true,
            X509Certificate2Collection certificateCollection = null)
        {
            AsDirectConnection(Environment.UserDomainName, Environment.UserName, port, certificateValidation, certificateCollection);
        }

        public void AsDirectConnection(string userDirectory, string userId, int port = 4242, bool certificateValidation = true,
            X509Certificate2Collection certificateCollection = null)
        {
            ConnectionType = ConnectionType.DirectConnection;
            var uriBuilder = new UriBuilder(BaseUri) {Port = port};
            BaseUri = uriBuilder.Uri;
            UserId = userId;
            UserDirectory = userDirectory;
            CertificateValidation = certificateValidation;
            Certificates = certificateCollection;
            var userHeaderValue = string.Format("UserDirectory={0};UserId={1}", UserDirectory, UserId);
            CustomHeaders.Add("X-Qlik-User", userHeaderValue);
            _isConfigured = true;
        }

        public void AsNtlmUserViaProxy(bool certificateValidation = true)
        {
            UserId = Environment.UserName;
            UserDirectory = Environment.UserDomainName;
            AsNtlmUserViaProxy(CredentialCache.DefaultNetworkCredentials, certificateValidation);
        }

        public void AsNtlmUserViaProxy(NetworkCredential credential, bool certificateValidation = true)
        {
            ConnectionType = ConnectionType.NtlmUserViaProxy;
            CertificateValidation = certificateValidation;
            if (credential != null)
            {
                var credentialCache = new CredentialCache();
                credentialCache.Add(this.BaseUri, "ntlm", credential);
                CustomCredential = credentialCache;
            }
            CustomHeaders.Add("User-Agent", "Windows");
            _isConfigured = true;
        }

        public void AsStaticHeaderUserViaProxy(string userId, string headerName, bool certificateValidation)
        {
            ConnectionType = ConnectionType.StaticHeaderUserViaProxy;
            UserId = userId;
            UserDirectory = Environment.UserDomainName;
            StaticHeaderName = headerName;
            CustomHeaders.Add(headerName, userId);
            _isConfigured = true;
        }

        public void Validate()
        {
            if (!_isConfigured)
                throw new RestClient.ConnectionNotConfiguredException();
            if (ConnectionType == ConnectionType.DirectConnection && Certificates == null)
                throw new RestClient.CertificatesNotLoadedException();
        }
    }
}