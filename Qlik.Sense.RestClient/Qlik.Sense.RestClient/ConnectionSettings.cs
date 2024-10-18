using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Qlik.Sense.RestClient
{
    internal class ConnectionSettings
    {
        public Uri BaseUri { get; set; }

        public CookieContainer CookieJar { get; set; }
        public bool IsAuthenticated { get; set; }

        public bool AllowAutoRedirect = true;
        public bool IsQcs = false;
        public string UserDirectory;
        public string UserId;
        public string ClientCredentialsEncoded;
        public ICredentials CustomCredential;
        public TimeSpan Timeout;
        public string Xrfkey;
        public IWebProxy Proxy { get; set; }
        public Dictionary<string, string> CustomHeaders { get; private set; } = new Dictionary<string, string>();
        public string CustomUserAgent { get; set; }

        public Dictionary<string, string> DefaultArguments { get; } = new Dictionary<string, string>();

        public bool CertificateValidation = true;
        public X509Certificate2Collection Certificates;
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
                throw;
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
                AllowAutoRedirect = this.AllowAutoRedirect,
                IsQcs = this.IsQcs,
                UserDirectory = this.UserDirectory,
                UserId = this.UserId,
                CertificateValidation = this.CertificateValidation,
                Certificates = this.Certificates,
                CustomCredential = this.CustomCredential,
                Timeout = this.Timeout,
                Xrfkey = this.Xrfkey,
                Proxy = this.Proxy,
                CustomHeaders = new Dictionary<string, string>(this.CustomHeaders),
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

        public ConnectionSettings(Uri uri) : this()
        {
            Timeout = System.Threading.Timeout.InfiniteTimeSpan;
            CookieJar = new CookieContainer();
            BaseUri = uri;
        }

        public ConnectionSettings(string uri) : this(new Uri(uri))
        {
        }

        public void SetClientCredentials(string clientId, string clientSecret)
        {
            ClientCredentialsEncoded = Base64Encode(clientId + ":" + clientSecret);
        }

        private static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        public QcsSessionInfo SessionInfo => new QcsSessionInfo(
            GetCookie("eas.sid")?.Value,
            GetCookie("eas.sid.sig")?.Value,
            GetCookie("_csrfToken")?.Value
        );

        internal Cookie GetCookie(string name)
        {
            return GetCookies()[name];
        }

        internal CookieCollection GetCookies()
        {
            return CookieJar.GetCookies(BaseUri);
        }
    }

    public class QcsSessionInfo
    {
        public string EasSid { get; }
        public string EasSidSig { get; }
        public string SessionToken { get; }

        public QcsSessionInfo(string easSid, string easSidSig, string sessionToken)
        {
            EasSid = easSid;
            EasSidSig = easSidSig;
            SessionToken = sessionToken;
        }

        public JObject GetJObject()
        {
            return JObject.FromObject(this);
        }
    }
}