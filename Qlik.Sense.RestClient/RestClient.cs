using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Qlik.Sense.RestClient.Qrs;

namespace Qlik.Sense.RestClient
{
    public class RestClient : IRestClient
    {
        internal static DebugConsole DebugConsole { private get; set; }

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
        public Dictionary<string, string> CustomHeaders => _connectionSettings.CustomHeaders;

        private User _user;
        public User User => _user ?? (_user = new User{Directory = UserDirectory, Id = UserId});
        public string Url => _connectionSettings.BaseUri.AbsoluteUri;
        public string UserId => _connectionSettings.UserId;
        public string UserDirectory => _connectionSettings.UserDirectory;

        public Uri BaseUri => _connectionSettings.BaseUri;

        private readonly ConnectionSettings _connectionSettings;

        public ConnectionType CurrentConnectionType => _connectionSettings.ConnectionType;

        private readonly Lazy<SenseHttpClient> _client;

        private RestClient(ConnectionSettings settings)
        {
            _connectionSettings = settings;
            _client = new Lazy<SenseHttpClient>(() => new SenseHttpClient(_connectionSettings));
        }

        public RestClient(string uri) : this(new ConnectionSettings(uri))
        {
            _connectionSettings.AuthenticationFunc = CollectCookieAsync;
        }

        public IRestClient ConnectAsQmc()
        {
            var client = new RestClient(_connectionSettings.Clone());
            client.CustomHeaders["X-Qlik-Security"] = "Context=ManagementAccess";
            return client;
        }

        public IRestClient ConnectAsHub()
        {
            var client = new RestClient(_connectionSettings.Clone());
            client.CustomHeaders["X-Qlik-Security"] = "Context=AppAccess";
            return client;
        }

        public IRestClient WithXrfkey(string xrfkey)
        {
            var client = new RestClient(_connectionSettings.Clone());
            client._connectionSettings.SetXrfKey(xrfkey);
            return client;
        }

        public IRestClient WithContentType(string contentType)
        {
            var client = new RestClient(_connectionSettings.Clone());
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
                DebugConsole?.Log("Authentication failed: " + e.InnerException?.Message);
                return false;
            }
            catch (Exception e)
            {
                DebugConsole?.Log("Authentication failed: " + e.Message);
                return false;
            }
        }

        public void AsDirectConnection(int port = 4242, bool certificateValidation = true,
            X509Certificate2Collection certificateCollection = null)
        {
            _connectionSettings.AsDirectConnection(port, certificateValidation, certificateCollection);
        }

        public void AsDirectConnection(string userDirectory, string userId, int port = 4242,
            bool certificateValidation = true, X509Certificate2Collection certificateCollection = null)
        {
            _connectionSettings.AsDirectConnection(userDirectory, userId, port, certificateValidation, certificateCollection);
        }

        public void AsJwtViaProxy(string key, bool certificateValidation = true)
        {
            _connectionSettings.AsJwtViaProxy(key, certificateValidation);
        }

        [Obsolete("Use method AsJwtViaProxy.")] // Obsolete since June 2020 
        public void AsJwtTokenViaProxy(string key, bool certificateValidation = true)
        {
            AsJwtViaProxy(key, certificateValidation);
        }

        public void AsJwtViaQcs(string key)
        {
            _connectionSettings.AsJwtViaQcs(key);
        }

        [Obsolete("Use method AsJwtViaQcs.")] // Obsolete since May 2020
        public void AsJwtTokenViaQcs(string key)
        {
            AsJwtViaQcs(key);
        }

        public void AsNtlmUserViaProxy(NetworkCredential credentials, bool certificateValidation = true)
        {
            _connectionSettings.AsNtlmUserViaProxy(credentials, certificateValidation);
        }

        public void AsNtlmUserViaProxy(bool certificateValidation = true)
        {
            _connectionSettings.AsNtlmUserViaProxy(certificateValidation);
        }

        public void AsStaticHeaderUserViaProxy(string userId, string headerName, bool certificateValidation = true)
        {
            _connectionSettings.AsStaticHeaderUserViaProxy(userId, headerName, certificateValidation);
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
            DebugConsole?.Log($"Calling:\t{method} {endpoint}");
        }

        private static string LogReceive(string message)
        {
            DebugConsole?.Log($"Receiving:\t{message}");
            return message;
        }

        private static byte[] LogReceive(byte[] data)
        {
            DebugConsole?.Log($"Receiving binary data of size {data.Length}");
            return data;
        }

        private static async Task<string> LogReceive(Task<string> messageTask)
        {
            var message = await messageTask.ConfigureAwait(false);
            DebugConsole?.Log($"Receiving:\t{message}");
            return message;
        }

        private static async Task<byte[]> LogReceive(Task<byte[]> messageTask)
        {
            var data = await messageTask.ConfigureAwait(false);
            DebugConsole?.Log($"Receiving binary data of size {data.Length}");
            return data;
        }

        public static X509Certificate2Collection LoadCertificateFromDirectory(string path)
        {
            return LoadCertificateFromDirectory(path, p => new X509Certificate2(p));
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

        private string PerformUploadStringAccess(string method, string endpoint, string body)
        {
            var task = PerformUploadStringAccessAsync(method, endpoint, body);
            task.ConfigureAwait(false);
            return task.Result;
        }

        private async Task<string> PerformUploadStringAccessAsync(string method, string endpoint, string body)
        {
            ValidateConfiguration();
            if (!await AuthenticateAsync())
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

        public T Post<T>(string endpoint, string body = "")
        {
            return JsonConvert.DeserializeObject<T>(Post(endpoint, body));
        }

        public T Post<T>(string endpoint, JToken body)
        {
            return Post<T>(endpoint, body.ToString(Formatting.None));
        }

        public Task<string> PostAsync(string endpoint, string body = "")
        {
            return PerformUploadStringAccessAsync("POST", endpoint, body);
        }

        public Task<string> PostAsync(string endpoint, JToken body)
        {
            return PostAsync(endpoint, body.ToString(Formatting.None));
        }

        public Task<T> PostAsync<T>(string endpoint, string body)
        {
            return PostAsync(endpoint, body).ContinueWith(t => JsonConvert.DeserializeObject<T>(t.Result));
        }

        public Task<T> PostAsync<T>(string endpoint, JToken body)
        {
            return PostAsync<T>(endpoint, body.ToString(Formatting.None));
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

        public string Put(string endpoint, string body)
        {
            return PerformUploadStringAccess("PUT", endpoint, body);
        }

        public string Put(string endpoint, JToken body)
        {
            return Put(endpoint, body.ToString(Formatting.None));
        }

        public T Put<T>(string endpoint, string body)
        {
            return JsonConvert.DeserializeObject<T>(Post(endpoint, body));
        }

        public T Put<T>(string endpoint, JToken body)
        {
            return Put<T>(endpoint, body.ToString(Formatting.None));
        }

        public Task<string> PutAsync(string endpoint, string body)
        {
            return PerformUploadStringAccessAsync("PUT", endpoint, body);
        }

        public Task<string> PutAsync(string endpoint, JToken body)
        {
            return PutAsync(endpoint, body.ToString(Formatting.None));
        }

        public Task<T> PutAsync<T>(string endpoint, string body)
        {
            return PutAsync(endpoint, body).ContinueWith(t => JsonConvert.DeserializeObject<T>(t.Result));
        }

        public Task<T> PutAsync<T>(string endpoint, JToken body)
        {
            return PutAsync<T>(endpoint, body.ToString(Formatting.None));
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
            _connectionSettings.Validate();
        }

        private async Task CollectCookieAsync()
        {
            DebugConsole?.Log($"Authenticating (calling GET /qrs/about)");
            var client = GetClient();
            await LogReceive(client.GetStringAsync(BaseUri.Append("/qrs/about"))).ConfigureAwait(false);
            DebugConsole?.Log($"Authentication complete.");
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
}