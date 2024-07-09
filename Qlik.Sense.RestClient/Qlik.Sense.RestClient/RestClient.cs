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
using Qlik.Sense.RestClient.Qrs;

namespace Qlik.Sense.RestClient
{
	public enum ConnectionType
	{
        Undefined,
		DirectConnection,
		NtlmUserViaProxy,
		StaticHeaderUserViaProxy,
		AnonymousViaProxy,
		JwtTokenViaProxy,
		JwtTokenViaQcs,
		ApiKeyViaQcs,
		ClientCredentialsViaQcs,
		ExistingSessionViaProxy,
		ExistingSessionViaQcs
	}

    public class RestClient : IRestClient
    {
        internal static RestClientDebugConsole RestClientDebugConsole { private get; set; }

        public static int MaximumConcurrentCalls
        {
            get => ServicePointManager.DefaultConnectionLimit;
            set => ServicePointManager.DefaultConnectionLimit = value;
        }

        public TimeSpan Timeout
        {
            get => _connectionSettings.Timeout;
            set => _connectionSettings.Timeout = value;
        }

        public IWebProxy Proxy
        {
            get => _connectionSettings.Proxy;
            set => _connectionSettings.Proxy = value;
        }

        public Dictionary<string, string> CustomHeaders => _connectionSettings.CustomHeaders;

        /// <summary>
        /// Custom HTTP user-agent header for identifying application.
        /// </summary>
        public string CustomUserAgent
        {
            get => _connectionSettings.CustomUserAgent;
            set => _connectionSettings.CustomUserAgent = value;
        }
        private User _user;
        public User User => _user ?? (_user = new User { Directory = UserDirectory, Id = UserId });
        public string Url => _connectionSettings.BaseUri.AbsoluteUri;
        public string UserId => _connectionSettings.UserId;
        public string UserDirectory => _connectionSettings.UserDirectory;

        public QcsSessionInfo QcsSessionInfo => _connectionSettings.SessionInfo;

        public Uri BaseUri => _connectionSettings.BaseUri;

        private readonly ConnectionSettings _connectionSettings;

        public ConnectionType CurrentConnectionType => _connectionType;

        private ConnectionType _connectionType = ConnectionType.Undefined;

        private bool IsConfigured => _connectionType != ConnectionType.Undefined;

        public Cookie GetCookie(string name)
        {
	        return _connectionSettings.GetCookie(name);
        }

        public CookieCollection GetCookies()
        {
	        return _connectionSettings.GetCookies();
        }

		private readonly Lazy<SenseHttpClient> _client;

		private RestClient()
		{
			_client = new Lazy<SenseHttpClient>(() => new SenseHttpClient(_connectionSettings));
		}

		private RestClient(RestClient source) : this()
		{
			_client = new Lazy<SenseHttpClient>(() => new SenseHttpClient(_connectionSettings));
			_connectionSettings = source._connectionSettings;
            _stats = source._stats;
            _connectionType = source._connectionType;
		}

        /// <summary>
        /// Experimental
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="stats"></param>
        public RestClient(string uri, Statistics stats) : this(uri)
        {
            _stats = stats;
        }

        public RestClient(Uri uri) : this()
        {
	        _connectionSettings = new ConnectionSettings(uri);
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

        public bool Authenticate()
        {
            var t = AuthenticateAsync();
            t.ConfigureAwait(false);
            return t.Result;
        }

        public async Task<bool> AuthenticateAsync()
        {
            if (_connectionSettings.IsAuthenticated)
                return true;

            try
            {
                await _connectionSettings.PerformAuthentication().ConfigureAwait(false);
                return true;
            }
            catch (AggregateException e)
            {
                RestClientDebugConsole?.Log("Authentication failed: " + e.InnerException?.Message);
                return false;
            }
            catch (Exception e)
            {
                RestClientDebugConsole?.Log("Authentication failed: " + e.Message);
                return false;
            }
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

		[Obsolete("Use method AsJwtViaProxy.")] // Obsolete since June 2020 
        public void AsJwtTokenViaProxy(string key, bool certificateValidation = true)
        {
            AsJwtViaProxy(key, certificateValidation);
        }

        private void AsJwtToken(string key)
        {
	        CustomHeaders.Add("Authorization", "Bearer " + key);
	        _connectionSettings.IsAuthenticated = false;
        }

		public void AsApiKeyViaQcs(string apiKey)
        {
			_connectionType = ConnectionType.ApiKeyViaQcs;
			_connectionSettings.AllowAutoRedirect = false;
			_connectionSettings.IsQcs = true;
			AsJwtToken(apiKey);
			_connectionSettings.IsAuthenticated = true;
        }

		public void AsJsonWebTokenViaQcs(string key)
        {
            AsJwtToken(key);
            _connectionSettings.IsQcs = true;
            _connectionSettings.AuthenticationFunc = CollectCookieJwtViaQcsAsync;
        }

        public void AsClientCredentialsViaQcs(string clientId, string clientSecret)
        {
	        _connectionType = ConnectionType.ClientCredentialsViaQcs;
			_connectionSettings.AsClientCredentialsViaQcs(clientId, clientSecret);
			_connectionSettings.AllowAutoRedirect = false;
			_connectionSettings.IsQcs = true;
			_connectionSettings.AuthenticationFunc = CollectAccessTokenViaOauthAsync;
        }


        [Obsolete("Use method AsApiKeyViaQcs.")] // Obsolete since September 2021
        public void AsJwtViaQcs(string key)
        {
            AsApiKeyViaQcs(key);
        }

        [Obsolete("Use method AsApiKeyViaQcs.")] // Obsolete since May 2020
        public void AsJwtTokenViaQcs(string key)
        {
            AsJwtViaQcs(key);
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

        private static void LogCall(string method, string endpoint)
        {
            RestClientDebugConsole?.Log($"Calling:\t{method} {endpoint}");
        }

        private static string LogReceive(string message)
        {
            RestClientDebugConsole?.Log($"Receiving:\t{message}");
            return message;
        }

        private static byte[] LogReceive(byte[] data)
        {
            RestClientDebugConsole?.Log($"Receiving binary data of size {data.Length}");
            return data;
        }

        private static Stream LogReceive(Stream stream)
        {
            RestClientDebugConsole?.Log($"Receiving data stream");
            return stream;
        }

        private static async Task<string> LogReceive(Task<string> messageTask)
        {
            var message = await messageTask.ConfigureAwait(false);
            RestClientDebugConsole?.Log($"Receiving:\t{message}");
            return message;
        }

        private static async Task<byte[]> LogReceive(Task<byte[]> messageTask)
        {
            var data = await messageTask.ConfigureAwait(false);
            RestClientDebugConsole?.Log($"Receiving binary data of size {data.Length}");
            return data;
        }

        private static async Task<Stream> LogReceive(Task<Stream> streamTask)
        {
            var stream = await streamTask.ConfigureAwait(false);
            RestClientDebugConsole?.Log($"Receiving data stream");
            return stream;
        }

        private HttpResponseMessage LogReceive(HttpResponseMessage rsp)
        {
	        RestClientDebugConsole?.Log(PrintHttpResponseLog(rsp));
	        return rsp;
        }

        private static async Task<HttpResponseMessage> LogReceive(Task<HttpResponseMessage> streamTask)
        {
	        var rsp = await streamTask.ConfigureAwait(false);
	        RestClientDebugConsole?.Log(PrintHttpResponseLog(rsp));
	        return rsp;
        }

        private static string PrintHttpResponseLog(HttpResponseMessage rsp)
        {
	        var contents = new[]
	        {
		        "Status Code:    " + (int) rsp.StatusCode + " (" + rsp.StatusCode + ")",
		        "Content length: " + rsp.Content.Headers.ContentLength,
		        "Content type:   " + rsp.Content.Headers.ContentType
	        }.Select(str => "   " + str).ToArray();

	        return $"Receiving HTTP response:\n" + string.Join(Environment.NewLine, contents);
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

        private SenseHttpClient GetClient()
        {
            return _client.Value;
        }

        public string Get(string endpoint)
        {
            ValidateConfiguration();
            if (!Authenticate())
                throw new AuthenticationException("Authentication failed.");
            LogCall("GET", endpoint);
            var client = GetClient();
            var task = client.GetStringAsync(BaseUri.Append(endpoint));
            task.ConfigureAwait(false);
            return LogReceive(task.Result);
        }

        private readonly Statistics _stats = new Statistics();

        /// <summary>
        /// Experimental
        /// </summary>
        public void PrintStats()
        {
            Console.WriteLine(_stats);
        }

        /// <summary>
        /// Experimental
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        public Result GetEx(string endpoint)
        {
            ValidateConfiguration();
            if (!Authenticate())
                throw new AuthenticationException("Authentication failed.");
            LogCall("GET", endpoint);
            var client = GetClient();
            var task = Result.CreateAsync(() => client.GetHttpAsync(BaseUri.Append(endpoint), false), _stats.Add);
            task.ConfigureAwait(false);
            var rsp = task.Result;
            LogReceive(rsp.Body);
            return rsp;
        }

        public T Get<T>(string endpoint)
        {
            return JsonConvert.DeserializeObject<T>(Get(endpoint));
        }

        public async Task<string> GetAsync(string endpoint)
        {
            ValidateConfiguration();
            if (!await AuthenticateAsync().ConfigureAwait(false))
                throw new AuthenticationException("Authentication failed.");
            LogCall("GET", endpoint);
            var client = GetClient();
            return await LogReceive(client.GetStringAsync(BaseUri.Append(endpoint))).ConfigureAwait(false);
        }

        public Task<T> GetAsync<T>(string endpoint)
        {
            return GetAsync(endpoint).ContinueWith(t => JsonConvert.DeserializeObject<T>(t.Result));
        }

        public HttpResponseMessage GetHttp(string endpoint, bool throwOnFailure = true)
        {
	        var client = GetClient();
	        LogCall("GET", endpoint);
	        var task = client.GetHttpAsync(BaseUri.Append(endpoint), throwOnFailure);
	        task.ConfigureAwait(false);
	        return LogReceive(task.Result);
		}

        public Task<HttpResponseMessage> GetHttpAsync(string endpoint, bool throwOnFailure = true)
        {
	        LogCall("GET", endpoint);
	        var client = GetClient();
            return LogReceive(client.GetHttpAsync(BaseUri.Append(endpoint), throwOnFailure));
        }

        public byte[] GetBytes(string endpoint)
        {
            ValidateConfiguration();
            if (!Authenticate())
                throw new AuthenticationException("Authentication failed.");
            LogCall("GET", endpoint);
            var client = GetClient();
            var task = client.GetBytesAsync(BaseUri.Append(endpoint));
            task.ConfigureAwait(false);
            return LogReceive(task.Result);
        }

        public async Task<byte[]> GetBytesAsync(string endpoint)
        {
            ValidateConfiguration();
            if (!await AuthenticateAsync().ConfigureAwait(false))
                throw new AuthenticationException("Authentication failed.");
            LogCall("GET", endpoint);
            var client = GetClient();
            return await LogReceive(client.GetBytesAsync(BaseUri.Append(endpoint))).ConfigureAwait(false);
        }

        public Stream GetStream(string endpoint)
        {
            ValidateConfiguration();
            if (!Authenticate())
                throw new AuthenticationException("Authentication failed.");
            LogCall("GET", endpoint);
            var client = GetClient();
            var task = client.GetStreamAsync(BaseUri.Append(endpoint));
            task.ConfigureAwait(false);
            return LogReceive(task.Result);
        }

        public async Task<Stream> GetStreamAsync(string endpoint)
        {
            ValidateConfiguration();
            if (!await AuthenticateAsync().ConfigureAwait(false))
                throw new AuthenticationException("Authentication failed.");
            LogCall("GET", endpoint);
            var client = GetClient();
            return await LogReceive(client.GetStreamAsync(BaseUri.Append(endpoint))).ConfigureAwait(false);
        }

        private string PerformUploadStringAccess(string method, string endpoint, string body)
        {
            var task = PerformUploadStringAccessAsync(method, endpoint, body);
            task.ConfigureAwait(false);
            return task.Result;
        }

        private async Task<string> PerformUploadStringAccessAsync(string method, string endpoint, string body)
        {
            ValidateConfiguration();
            if (!await AuthenticateAsync().ConfigureAwait(false))
                throw new AuthenticationException("Authentication failed.");
            LogCall(method, endpoint);
            var client = GetClient();
            switch (method.ToUpper())
            {
                case "POST":
                    return await LogReceive(client.PostStringAsync(BaseUri.Append(endpoint), body)).ConfigureAwait(false);
                case "PUT":
                    return await LogReceive(client.PutStringAsync(BaseUri.Append(endpoint), body)).ConfigureAwait(false);
                case "DELETE":
                    return await LogReceive(client.DeleteAsync(BaseUri.Append(endpoint))).ConfigureAwait(false);
            }

            return await LogReceive(client.PostStringAsync(BaseUri.Append(endpoint), body)).ConfigureAwait(false);
        }

        public string Post(string endpoint, string body = "")
        {
            return PerformUploadStringAccess("POST", endpoint, body);
        }

        public string Post(string endpoint, JToken body)
        {
            return Post(endpoint, body.ToString(Formatting.None));
        }

        public string Post(string endpoint, HttpContent content)
        {
            var task = PostAsync(endpoint, content);
            task.ConfigureAwait(false);
            return task.Result;
        }

        public T Post<T>(string endpoint, string body = "")
        {
            return JsonConvert.DeserializeObject<T>(Post(endpoint, body));
        }

        public T Post<T>(string endpoint, JToken body)
        {
            return Post<T>(endpoint, body.ToString(Formatting.None));
        }

        public T Post<T>(string endpoint, HttpContent content)
        {
            return JsonConvert.DeserializeObject<T>(Post(endpoint, content));
        }

        public HttpResponseMessage PostHttp(string endpoint, HttpContent content, bool throwOnFailure = true)
        {
            var task = PostHttpAsync(endpoint, content, throwOnFailure);
            task.ConfigureAwait(false);
            return task.Result;
        }

        public Task<string> PostAsync(string endpoint, string body = "")
        {
            return PerformUploadStringAccessAsync("POST", endpoint, body);
        }

        public Task<string> PostAsync(string endpoint, JToken body)
        {
            return PostAsync(endpoint, body.ToString(Formatting.None));
        }

        public async Task<string> PostAsync(string endpoint, HttpContent content)
        {
            ValidateConfiguration();
            if (!await AuthenticateAsync().ConfigureAwait(false))
                throw new AuthenticationException("Authentication failed.");
            LogCall("POST", endpoint);
			var client = GetClient();
            return await LogReceive(client.PostHttpContentAsync(BaseUri.Append(endpoint), content)).ConfigureAwait(false);
        }

        public Task<T> PostAsync<T>(string endpoint, string body = "")
        {
            return PostAsync(endpoint, body).ContinueWith(t => JsonConvert.DeserializeObject<T>(t.Result));
        }

        public Task<T> PostAsync<T>(string endpoint, JToken body)
        {
            return PostAsync<T>(endpoint, body.ToString(Formatting.None));
        }

        public Task<T> PostAsync<T>(string endpoint, HttpContent content)
        {
            return PostAsync(endpoint, content).ContinueWith(t => JsonConvert.DeserializeObject<T>(t.Result));
        }

        public async Task<HttpResponseMessage> PostHttpAsync(string endpoint, string body = "", bool throwOnFailure = true)
        {
            ValidateConfiguration();
            if (!await AuthenticateAsync().ConfigureAwait(false))
                throw new AuthenticationException("Authentication failed.");
            LogCall("POST", endpoint);
			var client = GetClient();
            return await LogReceive(client.PostHttpAsync(BaseUri.Append(endpoint), body, throwOnFailure)).ConfigureAwait(false);
        }

        public Task<HttpResponseMessage> PostHttpAsync(string endpoint, JToken body, bool throwOnFailure = true)
        {
            return PostHttpAsync(endpoint, body.ToString(Formatting.None), throwOnFailure);
        }

        public async Task<HttpResponseMessage> PostHttpAsync(string endpoint, HttpContent content, bool throwOnFailure = true)
        {
            ValidateConfiguration();
            if (!await AuthenticateAsync().ConfigureAwait(false))
                throw new AuthenticationException("Authentication failed.");
            LogCall("POST", endpoint);
			var client = GetClient();
            return await LogReceive(client.PostHttpAsync(BaseUri.Append(endpoint), content, throwOnFailure)).ConfigureAwait(false);
        }

        public string Post(string endpoint, byte[] body)
        {
            var task = PostAsync(endpoint, body);
            task.ConfigureAwait(false);
            return task.Result;
        }

        public T Post<T>(string endpoint, byte[] body)
        {
            return JsonConvert.DeserializeObject<T>(Post(endpoint, body));
        }

        public async Task<string> PostAsync(string endpoint, byte[] body)
        {
            ValidateConfiguration();
            if (!await AuthenticateAsync().ConfigureAwait(false))
                throw new AuthenticationException("Authentication failed.");
            LogCall("POST", endpoint);
            var client = GetClient();
            return await LogReceive(client.PostDataAsync(BaseUri.Append(endpoint), body)).ConfigureAwait(false);
        }

        public Task<T> PostAsync<T>(string endpoint, byte[] body)
        {
            return PostAsync(endpoint, body).ContinueWith(t => JsonConvert.DeserializeObject<T>(t.Result));
        }

        public string Put(string endpoint, string body = "")
        {
            return PerformUploadStringAccess("PUT", endpoint, body);
        }

        public string Put(string endpoint, JToken body)
        {
            return Put(endpoint, body.ToString(Formatting.None));
        }

        public string Put(string endpoint, HttpContent content)
        {
            var task = PutAsync(endpoint, content);
            task.ConfigureAwait(false);
            return task.Result;
        }

        public T Put<T>(string endpoint, string body = "")
        {
            return JsonConvert.DeserializeObject<T>(Post(endpoint, body));
        }

        public T Put<T>(string endpoint, JToken body)
        {
            return Put<T>(endpoint, body.ToString(Formatting.None));
        }

        public T Put<T>(string endpoint, HttpContent content)
        {
            var task = PutAsync<T>(endpoint, content);
            task.ConfigureAwait(false);
            return task.Result;
        }

        public HttpResponseMessage PutHttp(string endpoint, HttpContent content, bool throwOnFailure = true)
        {
            var task = PutHttpAsync(endpoint, content, throwOnFailure);
            task.ConfigureAwait(false);
            return task.Result;
        }

        public Task<string> PutAsync(string endpoint, string body = "")
        {
            return PerformUploadStringAccessAsync("PUT", endpoint, body);
        }

        public Task<string> PutAsync(string endpoint, JToken body)
        {
            return PutAsync(endpoint, body.ToString(Formatting.None));
        }

        public async Task<string> PutAsync(string endpoint, HttpContent content)
        {
            ValidateConfiguration();
            if (!await AuthenticateAsync().ConfigureAwait(false))
                throw new AuthenticationException("Authentication failed.");
            var client = GetClient();
            return await client.PutHttpContentAsync(BaseUri.Append(endpoint), content).ConfigureAwait(false);
        }

        public Task<T> PutAsync<T>(string endpoint, string body = "")
        {
            return PutAsync(endpoint, body).ContinueWith(t => JsonConvert.DeserializeObject<T>(t.Result));
        }

        public Task<T> PutAsync<T>(string endpoint, JToken body)
        {
            return PutAsync<T>(endpoint, body.ToString(Formatting.None));
        }

        public Task<T> PutAsync<T>(string endpoint, HttpContent content)
        {
            return PutAsync(endpoint, content).ContinueWith(t => JsonConvert.DeserializeObject<T>(t.Result));
        }

        public async Task<HttpResponseMessage> PutHttpAsync(string endpoint, HttpContent content, bool throwOnFailure = true)
        {
            ValidateConfiguration();
            if (!await AuthenticateAsync().ConfigureAwait(false))
                throw new AuthenticationException("Authentication failed.");
            var client = GetClient();
            return await client.PutHttpAsync(BaseUri.Append(endpoint), content, throwOnFailure).ConfigureAwait(false);
        }

        public string Delete(string endpoint)
        {
            return PerformUploadStringAccess("DELETE", endpoint, "");
        }

        public Task<string> DeleteAsync(string endpoint)
        {
            return PerformUploadStringAccessAsync("DELETE", endpoint, "");
        }


        private void ValidateConfiguration()
        {
	        if (!IsConfigured)
		        throw new RestClient.ConnectionNotConfiguredException();
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
			AsJwtToken(token);
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
        
        public class ConnectionNotConfiguredException : Exception
        {
        }

        public class CertificatesNotLoadedException : Exception
        {
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