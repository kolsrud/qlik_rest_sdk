using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

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

        public string Url => _connectionSettings.BaseUri.AbsoluteUri;
        public Uri BaseUri => _connectionSettings.BaseUri;

        private readonly ConnectionSettings _connectionSettings;
        private bool _performCertificateValidation = true;

        public ConnectionType CurrentConnectionType => _connectionSettings.ConnectionType;

#if (NETCOREAPP2_1)
        private readonly HttpClientHandler _clientHandler;
#else
        private readonly WebRequestHandler _clientHandler;
#endif
        private readonly Lazy<SenseHttpClient> _client;

        private RestClient(ConnectionSettings settings)
        {
            _connectionSettings = settings;
#if (NETCOREAPP2_1)
            _clientHandler = new HttpClientHandler();
#else
            _clientHandler = new WebRequestHandler();
#endif
            _clientHandler.CookieContainer = _connectionSettings.CookieJar;
            _client = new Lazy<SenseHttpClient>(() => new SenseHttpClient(_connectionSettings, _clientHandler));
        }

        public RestClient(string uri) : this(new ConnectionSettings(uri))
        {
            _connectionSettings.AuthenticationFunc = CollectCookieAsync;
        }

        private void DeactivateCertificateValidation()
        {
            _performCertificateValidation = false;
#if (NETCOREAPP2_1)
            _clientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
#else
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
#endif
        }

        public IRestClient WithContentType(string contentType)
        {
            var client = new RestClient(_connectionSettings.Clone());
            client._connectionSettings.ContentType = contentType;
            if (!_performCertificateValidation)
                client.DeactivateCertificateValidation();
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
                await _connectionSettings.PerformAuthentication();
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
            if (!certificateValidation)
                DeactivateCertificateValidation();

            if (certificateCollection != null)
                _clientHandler.ClientCertificates.AddRange(certificateCollection);

            _connectionSettings.AsDirectConnection(port, certificateCollection);
        }

        public void AsDirectConnection(string userDirectory, string userId, int port = 4242,
            bool certificateValidation = true, X509Certificate2Collection certificateCollection = null)
        {
            if (!certificateValidation)
                DeactivateCertificateValidation();

            if (certificateCollection != null)
                _clientHandler.ClientCertificates.AddRange(certificateCollection);

            _connectionSettings.AsDirectConnection(userDirectory, userId, port, certificateCollection);
        }

        public void AsNtlmUserViaProxy(NetworkCredential credentials, bool certificateValidation = true)
        {
            if (!certificateValidation)
                DeactivateCertificateValidation();

            var credentialCache = new CredentialCache();
            credentialCache.Add(this.BaseUri, "ntlm", credentials);
            _clientHandler.Credentials = credentialCache;
            _clientHandler.CookieContainer = _connectionSettings.CookieJar;
            _connectionSettings.AsNtlmUserViaProxy(credentials);
        }

        public void AsNtlmUserViaProxy(bool certificateValidation = true)
        {
            AsNtlmUserViaProxy(CredentialCache.DefaultCredentials.GetCredential(this.BaseUri, "ntlm"), certificateValidation);
        }

        public void AsStaticHeaderUserViaProxy(string userId, string headerName, bool certificateValidation = true)
        {
            if (!certificateValidation)
                DeactivateCertificateValidation();

            _connectionSettings.AsStaticHeaderUserViaProxy(userId, headerName);
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
            DebugConsole?.Log($"Recieving:\t{message}");
            return message;
        }

        private static async Task<string> LogReceive(Task<string> messageTask)
        {
            var message = await messageTask;
            DebugConsole?.Log($"Recieving:\t{message}");
            return message;
        }

        public static X509Certificate2Collection LoadCertificateFromDirectory(string path,
            SecureString certificatePassword = null)
        {
            var clientCertPath = Path.Combine(path, "client.pfx");
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);
            if (!File.Exists(clientCertPath)) throw new FileNotFoundException(clientCertPath);
            var certificate = certificatePassword == null
                ? new X509Certificate2(clientCertPath)
                : new X509Certificate2(clientCertPath, certificatePassword);
            return new X509Certificate2Collection(certificate);
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

        public async Task<string> GetAsync(string endpoint)
        {
            ValidateConfiguration();
            if (!await AuthenticateAsync())
                throw new AuthenticationException("Authentication failed.");
            LogCall("GET", endpoint);
            var client = GetClient();
            return await LogReceive(client.GetStringAsync(BaseUri.Append(endpoint)));
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
                    return await LogReceive(client.PostStringAsync(BaseUri.Append(endpoint), body));
                case "PUT":
                    return await LogReceive(client.PutStringAsync(BaseUri.Append(endpoint), body));
                case "DELETE":
                    return await LogReceive(client.DeleteAsync(BaseUri.Append(endpoint)));
            }

            return await LogReceive(client.PostStringAsync(BaseUri.Append(endpoint), body));
        }

        public string Post(string endpoint, string body)
        {
            return PerformUploadStringAccess("POST", endpoint, body);
        }

        public Task<string> PostAsync(string endpoint, string body)
        {
            return PerformUploadStringAccessAsync("POST", endpoint, body);
        }

        public string Post(string endpoint, byte[] body)
        {
            var task = PostAsync(endpoint, body);
            task.ConfigureAwait(false);
            return task.Result;
        }

        public async Task<string> PostAsync(string endpoint, byte[] body)
        {
            ValidateConfiguration();
            if (!await AuthenticateAsync())
                throw new AuthenticationException("Authentication failed.");
            LogCall("POST", endpoint);
            var client = GetClient();
            return await LogReceive(client.PostDataAsync(BaseUri.Append(endpoint), body));
        }

        public string Put(string endpoint, string body)
        {
            return PerformUploadStringAccess("PUT", endpoint, body);
        }

        public Task<string> PutAsync(string endpoint, string body)
        {
            return PerformUploadStringAccessAsync("PUT", endpoint, body);
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
            await LogReceive(client.GetStringAsync(BaseUri.Append("/qrs/about")));
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