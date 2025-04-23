using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
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

    public interface IRestClientGeneric
    {
        string Url { get; }
		
        bool Authenticate();
        Task<bool> AuthenticateAsync();

        string Get(string endpoint);
        T Get<T>(string endpoint);
        Task<string> GetAsync(string endpoint);
        Task<T> GetAsync<T>(string endpoint);
        byte[] GetBytes(string endpoint);
        Task<byte[]> GetBytesAsync(string endpoint);
        Stream GetStream(string endpoint);
        Task<Stream> GetStreamAsync(string endpoint);
        HttpResponseMessage GetHttp(string endpoint, bool throwOnFailure = true);
        Task<HttpResponseMessage> GetHttpAsync(string endpoint, bool throwOnFailure = true);

        /// <summary>
        /// Experimental
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        Result GetEx(string endpoint);

        string Post(string endpoint, string body = "");
        string Post(string endpoint, JToken body);
        string Post(string endpoint, HttpContent content);
        T Post<T>(string endpoint, string body = "");
        T Post<T>(string endpoint, JToken body);
        T Post<T>(string endpoint, HttpContent content);
        HttpResponseMessage PostHttp(string endpoint, HttpContent content, bool throwOnFailure = true);
        Task<string> PostAsync(string endpoint, string body = "");
        Task<string> PostAsync(string endpoint, JToken body);
        Task<string> PostAsync(string endpoint, HttpContent content);
        Task<T> PostAsync<T>(string endpoint, string body = "");
        Task<T> PostAsync<T>(string endpoint, JToken body);
        Task<T> PostAsync<T>(string endpoint, HttpContent content);
        Task<HttpResponseMessage> PostHttpAsync(string endpoint, string body = "", bool throwOnFailure = true);
        Task<HttpResponseMessage> PostHttpAsync(string endpoint, JToken body, bool throwOnFailure = true);
        Task<HttpResponseMessage> PostHttpAsync(string endpoint, HttpContent content, bool throwOnFailure = true);

        string Post(string endpoint, byte[] body);
        T Post<T>(string endpoint, byte[] body);
        Task<string> PostAsync(string endpoint, byte[] body);
        Task<T> PostAsync<T>(string endpoint, byte[] body);

        string Put(string endpoint, string body = "");
        string Put(string endpoint, JToken body);
        string Put(string endpoint, HttpContent content);
        T Put<T>(string endpoint, string body = "");
        T Put<T>(string endpoint, JToken body);
        T Put<T>(string endpoint, HttpContent content);
        HttpResponseMessage PutHttp(string endpoint, HttpContent content, bool throwOnFailure = true);
        Task<string> PutAsync(string endpoint, string body = "");
        Task<string> PutAsync(string endpoint, JToken body);
        Task<T> PutAsync<T>(string endpoint, string body = "");
        Task<T> PutAsync<T>(string endpoint, JToken body);
        Task<T> PutAsync<T>(string endpoint, HttpContent content);
        Task<HttpResponseMessage> PutHttpAsync(string endpoint, HttpContent content, bool throwOnFailure = true);

        string Delete(string endpoint);
        Task<string> DeleteAsync(string endpoint);
    }

    public class RestClientGeneric : IRestClientGeneric
    {
        internal static RestClientDebugConsole RestClientDebugConsole { get; set; }

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

        internal readonly ConnectionSettings _connectionSettings;

        public ConnectionType CurrentConnectionType => _connectionType;

        protected internal ConnectionType _connectionType = ConnectionType.Undefined;

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

        protected RestClientGeneric()
        {
            _client = new Lazy<SenseHttpClient>(() => new SenseHttpClient(_connectionSettings));
        }

        protected RestClientGeneric(RestClientGeneric source)
        {
            _connectionSettings = source._connectionSettings.Clone();
            _client = new Lazy<SenseHttpClient>(() => new SenseHttpClient(_connectionSettings));
            _stats = source._stats;
            _connectionType = source._connectionType;
        }

        /// <summary>
        /// Experimental
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="stats"></param>
        public RestClientGeneric(string uri, Statistics stats) : this(uri)
        {
            _stats = stats;
        }

        public RestClientGeneric(Uri uri) : this()
        {
            _connectionSettings = new ConnectionSettings(uri);
        }

        public RestClientGeneric(string uri) : this(new Uri(uri))
        {
        }

        private readonly Statistics _stats = new Statistics();

        /// <summary>
        /// Experimental
        /// </summary>
        public void PrintStats()
        {
            Console.WriteLine(_stats);
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

        protected internal static async Task<string> LogReceive(Task<string> messageTask)
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
                "Status Code:    " + (int)rsp.StatusCode + " (" + rsp.StatusCode + ")",
                "Content length: " + rsp.Content.Headers.ContentLength,
                "Content type:   " + rsp.Content.Headers.ContentType
            }.Select(str => "   " + str).ToArray();

            return $"Receiving HTTP response:\n" + string.Join(Environment.NewLine, contents);
        }

        protected internal SenseHttpClient GetClient()
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
            return JsonConvert.DeserializeObject<T>(Put(endpoint, body));
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

        public class ConnectionNotConfiguredException : Exception
        {
        }

        public class CertificatesNotLoadedException : Exception
        {
        }
    }
}