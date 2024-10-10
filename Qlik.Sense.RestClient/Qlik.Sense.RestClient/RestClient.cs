using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Qlik.Sense.RestClient
{
    public class RestClient : RestClientGeneric, IRestClient
    {
		private RestClient(RestClient source) : base(source)
		{
		}

        /// <summary>
        /// Experimental
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="stats"></param>
        public RestClient(string uri, Statistics stats) : base(uri, stats)
        {
            _connectionSettings.AuthenticationFunc = CollectCookieAsync;
        }

        public RestClient(Uri uri) : base(uri)
        {
            _connectionSettings.AuthenticationFunc = CollectCookieAsync;
        }

        public RestClient(string uri) : this(new Uri(uri))
        {
        }

        public IRestClient ConnectAsQmc()
        {
            var client = new RestClient(this);
            client.CustomHeaders["X-Qlik-Security"] = "Context=ManagementAccess";
            return client;
        }

        public IRestClient ConnectAsHub()
        {
	        var client = new RestClient(this);
            client.CustomHeaders["X-Qlik-Security"] = "Context=AppAccess";
            return client;
        }

        public IRestClient WithXrfkey(string xrfkey)
        {
            var client = new RestClient(this);
            client._connectionSettings.SetXrfKey(xrfkey);
            return client;
        }

        public IRestClient WithContentType(string contentType)
        {
            var client = new RestClient(this);
            client._connectionSettings.ContentType = contentType;
            return client;
        }

        public void AsDirectConnection(int port = 4242, bool certificateValidation = true,
            X509Certificate2Collection certificateCollection = null)
        {
            AsDirectConnection(Environment.UserDomainName, Environment.UserName, port, certificateValidation, certificateCollection);
        }

        public void AsDirectConnection(string userDirectory, string userId, int port = 4242,
            bool certificateValidation = true, X509Certificate2Collection certificateCollection = null)
        {
	        if (certificateCollection == null)
		        throw new ArgumentNullException(nameof(certificateCollection));
	        _connectionType = ConnectionType.DirectConnection;
	        var uriBuilder = new UriBuilder(BaseUri) { Port = port };
	        _connectionSettings.BaseUri = uriBuilder.Uri;
	        _connectionSettings.UserId = userId;
	        _connectionSettings.UserDirectory = userDirectory;
	        _connectionSettings.CertificateValidation = certificateValidation;
	        _connectionSettings.Certificates = certificateCollection;
	        var userHeaderValue = string.Format("UserDirectory={0};UserId={1}", UserDirectory, UserId);
	        CustomHeaders.Add("X-Qlik-User", userHeaderValue);
	        _connectionSettings.IsAuthenticated = true;
        }

        public void AsJwtViaProxy(string key, bool certificateValidation = true)
        {
	        _connectionType = ConnectionType.JwtTokenViaProxy;
            _connectionSettings.CertificateValidation = certificateValidation;
            AsJwtToken(key);
        }

        private void AsJwtToken(string key)
        {
	        CustomHeaders.Add("Authorization", "Bearer " + key);
	        _connectionSettings.IsAuthenticated = false;
        }

        [Obsolete("Use method IRestClientQcs.AsApiKey")] // Obsolete since October 2024, v2.0.0
        public void AsApiKeyViaQcs(string apiKey)
        {
			_connectionType = ConnectionType.ApiKeyViaQcs;
			_connectionSettings.AllowAutoRedirect = false;
			_connectionSettings.IsQcs = true;
			AsJwtToken(apiKey);
			_connectionSettings.IsAuthenticated = true;
        }

        [Obsolete("Use method IRestClientQcs.AsJwt")] // Obsolete since October 2024, v2.0.0
        public void AsJsonWebTokenViaQcs(string key)
        {
            AsJwtToken(key);
            _connectionSettings.IsQcs = true;
            _connectionSettings.AuthenticationFunc = CollectCookieJwtViaQcsAsync;
        }

        [Obsolete("Use method IRestClientQcs.AsApiKey and acquire access token separately.")] // Obsolete since October 2024, v2.0.0
        public void AsClientCredentialsViaQcs(string clientId, string clientSecret)
        {
	        _connectionType = ConnectionType.ClientCredentialsViaQcs;
            _connectionSettings.SetClientCredentials(clientId, clientSecret);
			_connectionSettings.IsAuthenticated = false;
			_connectionSettings.AllowAutoRedirect = false;
			_connectionSettings.IsQcs = true;
			_connectionSettings.AuthenticationFunc = CollectAccessTokenViaOauthAsync;
        }

        private async Task CollectCookieAsync()
        {
            RestClientDebugConsole?.Log($"Authenticating (calling GET /qrs/about)");
            var client = GetClient();
            await LogReceive(client.GetStringAsync(BaseUri.Append("/qrs/about"))).ConfigureAwait(false);
            RestClientDebugConsole?.Log($"Authentication complete.");
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

        private async Task CollectAccessTokenViaOauthAsync()
        {
            var token = await GetAccessTokenAsync().ConfigureAwait(false);
            _connectionType = ConnectionType.ApiKeyViaQcs;
            CustomHeaders.Add("Authorization", "Bearer " + token);
            _connectionSettings.IsAuthenticated = true;
            _connectionSettings.AllowAutoRedirect = false;
            _connectionSettings.IsQcs = true;
            RestClientDebugConsole?.Log($"Authentication complete.");
        }

        private async Task<string> GetAccessTokenAsync()
        {
            var endpoint = "/oauth/token";
            RestClientDebugConsole?.Log($"Authenticating (calling POST {endpoint})");
            var body = JToken.FromObject(new
            {
                scope = "user_default",
                grant_type = "client_credentials"
            });

            var client = new SenseHttpClient(_connectionSettings.Clone());
            client.AddDefaultHeader("Authorization", "Basic " + _connectionSettings.ClientCredentialsEncoded);
            try
            {
                var rsp = await client.PostStringAsync(BaseUri.Append(endpoint), body.ToString(Formatting.None)).ConfigureAwait(false);
                var rspJson = JObject.Parse(rsp);
                return rspJson["access_token"].Value<string>();
            }
            catch (Exception e)
            {
                throw new AuthenticationException("Failed to retrieve access token.", e);
            }
        }

        public void AsNtlmUserViaProxy(bool certificateValidation = true)
        {
	        _connectionSettings.UserId = Environment.UserName;
	        _connectionSettings.UserDirectory = Environment.UserDomainName;
	        AsNtlmUserViaProxy(CredentialCache.DefaultNetworkCredentials, certificateValidation);
        }

        public void AsNtlmUserViaProxy(NetworkCredential credential, bool certificateValidation = true)
        {
	        _connectionType = ConnectionType.NtlmUserViaProxy;
			_connectionSettings.CertificateValidation = certificateValidation;
	        if (credential != null)
	        {
		        var credentialCache = new CredentialCache();
		        credentialCache.Add(this.BaseUri, "ntlm", credential);
		        _connectionSettings.CustomCredential = credentialCache;
	        }
	        CustomHeaders.Add("User-Agent", "Windows");
        }

        public void AsAnonymousUserViaProxy(bool certificateValidation = true)
        {
	        _connectionType = ConnectionType.AnonymousViaProxy;
	        _connectionSettings.CertificateValidation = certificateValidation;
	        _connectionSettings.IsAuthenticated = true;
        }

        public void AsStaticHeaderUserViaProxy(string userId, string headerName, bool certificateValidation = true)
        {
	        _connectionType = ConnectionType.StaticHeaderUserViaProxy;
            _connectionSettings.CertificateValidation = certificateValidation;
            _connectionSettings.UserId = userId;
            // Todo: This is incorrect. We do not know the UserDirectory in this case. That's controlled by the virtual proxy.
            _connectionSettings.UserDirectory = Environment.UserDomainName;
            _connectionSettings.CustomHeaders.Add(headerName, userId);
		}

		public void AsExistingSessionViaProxy(string sessionId, string cookieHeaderName, bool proxyUsesSsl = true, bool certificateValidation = true)
        {
	        _connectionType = ConnectionType.ExistingSessionViaProxy;
			_connectionSettings.CertificateValidation = certificateValidation;
			_connectionSettings.CookieJar.Add(new Cookie(cookieHeaderName, sessionId) { Domain = BaseUri.Host });
			_connectionSettings.IsAuthenticated = true;
        }

        [Obsolete("Use method IRestClientQcs.AsExistingSessionViaQcs")] // Obsolete since October 2024, v2.0.0
        public void AsExistingSessionViaQcs(QcsSessionInfo sessionInfo)
		{
			_connectionType = ConnectionType.ExistingSessionViaQcs;
			_connectionSettings.IsQcs = true;
			_connectionSettings.CookieJar.Add(BaseUri, new Cookie("eas.sid", sessionInfo.EasSid));
			_connectionSettings.CookieJar.Add(BaseUri, new Cookie("eas.sid.sig", sessionInfo.EasSidSig));
			CustomHeaders[SenseHttpClient.CSRF_TOKEN_ID] = sessionInfo.SessionToken;
		}

		public static X509Certificate2Collection LoadCertificateFromStore()
        {
            var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            var certificates = store.Certificates.Cast<X509Certificate2>().Where(c => c.FriendlyName == "QlikClient")
                .ToArray();
            store.Close();
            if (certificates.Any())
            {
                return new X509Certificate2Collection(certificates);
            }

            throw new CertificatesNotLoadedException();
        }

		public static X509Certificate2Collection LoadCertificateFromDirectory(string path)
        {
            return LoadCertificateFromDirectory(path, p => new X509Certificate2(p));
        }

        public static X509Certificate2Collection LoadCertificateFromDirectory(string path, string certificatePassword)
        {
            var pwd = new SecureString();
            certificatePassword.ToList().ForEach(pwd.AppendChar);
            return LoadCertificateFromDirectory(path, p => new X509Certificate2(p, pwd));
        }

        public static X509Certificate2Collection LoadCertificateFromDirectory(string path, SecureString certificatePassword)
        {
            return LoadCertificateFromDirectory(path, p => new X509Certificate2(p, certificatePassword));
        }

        public static X509Certificate2Collection LoadCertificateFromDirectory(string path, SecureString certificatePassword, X509KeyStorageFlags keyStorageFlags)
        {
            return LoadCertificateFromDirectory(path, p => new X509Certificate2(p, certificatePassword, keyStorageFlags));
        }

        public static X509Certificate2Collection LoadCertificateFromDirectory(string path, Func<string, X509Certificate2> f)
        {
            var clientCertPath = Path.Combine(path, "client.pfx");
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);
            if (!File.Exists(clientCertPath)) throw new FileNotFoundException(clientCertPath);
            return new X509Certificate2Collection(f(clientCertPath));
        }
    }

    public static class UriExtensions
    {
        public static Uri Append(this Uri uri, params string[] paths)
        {
            return new Uri(paths.Aggregate(uri.AbsoluteUri,
                (current, path) => string.Format("{0}/{1}", current.TrimEnd('/'), path.TrimStart('/'))));
        }
    }

    /// <summary>
    /// Experimental
    /// </summary>
    public class Statistics
    {
        public int Cnt;
        public int CntWithBody;
        public Dictionary<HttpStatusCode, int> CntPerStatusCode = new Dictionary<HttpStatusCode, int>();
        public TimeSpan TotalDuration = TimeSpan.Zero;
        public int TotalSize;

        /// <summary>
        /// Experimental
        /// </summary>
        public void Reset()
        {
            lock (this)
            {
                Cnt = 0;
                CntWithBody = 0;
                CntPerStatusCode = new Dictionary<HttpStatusCode, int>();
                TotalDuration = TimeSpan.Zero;
                TotalSize = 0;
            }
        }

        /// <summary>
        /// Experimental
        /// </summary>
        /// <param name="result"></param>
        public void Add(Result result)
        {
            lock (this)
            {
                Cnt++;
                if (CntPerStatusCode.ContainsKey(result.ReturnCode))
                    CntPerStatusCode[result.ReturnCode]++;
                else
                    CntPerStatusCode[result.ReturnCode] = 1;
                TotalDuration += result.Duration;
                if (result.Body != null)
                {
                    CntWithBody++;
                    TotalSize = result.Body.Length;
                }
            }
        }

        public override string ToString()
        {
            var strs = new List<string>
            {
                $"Total cnt:      {Cnt}"
            };
            if (Cnt > 0)
            {
                strs.AddRange(CntPerStatusCode.Select(kv =>
                    $"  |- Count for status code {kv.Key} : {kv.Value} ({CalcPercentage(kv.Value, Cnt)})"));
                strs.Add($"Total duration: {TotalDuration}");
                strs.Add($"Total size:     {TotalSize}{Result.PrintFormat(TotalSize)}");
                var avgDuration = TimeSpan.FromTicks(TotalDuration.Ticks / Cnt);
                strs.Add($"Avg duration:   {avgDuration}");
            }

            if (CntWithBody > 0)
            {
                var avgSize = (int)TotalSize / CntWithBody;
                strs.Add($"Avg size:       {avgSize}{Result.PrintFormat(avgSize)}");
            }

            return string.Join(Environment.NewLine, strs);
        }

        private static string CalcPercentage(int argValue, int cnt)
        {
            return $"{(double)argValue / cnt:P2}";
        }
    }

    /// <summary>
    /// Experimental
    /// </summary>
    public class Result
    {
        public string Body { get; private set; }
        public HttpStatusCode ReturnCode { get; private set; }
        public bool IsStatusSuccessCode => (int)ReturnCode >= 200 && (int)ReturnCode < 300;
        public TimeSpan TimeToResponse { get; private set; }
        public TimeSpan Duration { get; private set; }

        private Result()
        {
        }

        public override string ToString()
        {
            return ToString(true);
        }

        public string ToString(bool includeBody, Formatting formatting = Formatting.Indented)
        {
            var strs = new List<string>
            {
                $"IsSuccess:        {IsStatusSuccessCode}",
                $"Return code:      {ReturnCode} ({(int) ReturnCode})",
                $"Time to response: {TimeToResponse}",
                $"Time to download: {Duration-TimeToResponse}",
                $"Total duration:   {Duration}",
            };
            if (IsStatusSuccessCode)
                strs.Add($"Response size:    {Body.Length}{PrintFormat(Body.Length)}");
            if (includeBody && IsStatusSuccessCode)
            {
                strs.Add("--- Body ---");
                strs.Add(formatting == Formatting.None ? Body : JToken.Parse(Body).ToString());
            }

            return string.Join(Environment.NewLine, strs);
        }

        /// <summary>
        /// Experimental
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        public static string PrintFormat(int n)
        {
            if (n < 1024)
                return "";
            if (n > 1024 * 1024)
                return " (" + (n / (1024 * 1024)).ToString("N") + " MB)";
            return " (" + (n / 1024).ToString("N") + " KB)";
        }

        /// <summary>
        /// Experimental
        /// </summary>
        /// <param name="f"></param>
        /// <param name="statisticsCollector"></param>
        /// <returns></returns>
        public static async Task<Result> CreateAsync(Func<Task<HttpResponseMessage>> f, Action<Result> statisticsCollector)
        {
            var sw = new Stopwatch();
            sw.Start();
            var rsp = await f().ConfigureAwait(false);
            var result = new Result { ReturnCode = rsp.StatusCode, TimeToResponse = sw.Elapsed };
            if (rsp.IsSuccessStatusCode)
                result.Body = await rsp.Content.ReadAsStringAsync().ConfigureAwait(false);
            sw.Stop();
            result.Duration = sw.Elapsed;
            statisticsCollector(result);
            return result;
        }
    }
}