using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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

        public string Url => _connectionSettings.BaseUri.AbsoluteUri;
        public Uri BaseUri => _connectionSettings.BaseUri;

        private readonly ConnectionSettings _connectionSettings;

        public ConnectionType? CurrentConnectionType { get; private set; }

        public enum ConnectionType
        {
            NtlmUserViaProxy,
            StaticHeaderUserViaProxy,
            DirectConnection
        }

        private readonly Pool<WebClient> _clientPool;

        private RestClient(ConnectionSettings settings)
        {
            _connectionSettings = settings;
            _clientPool = new Pool<WebClient>(() => new SenseWebClient(_connectionSettings.Clone()));
        }

        public RestClient(string uri) : this(new ConnectionSettings(uri))
        {
        }

        public IRestClient WithContentType(string contentType)
        {
            return WithWebTransform(req => req.ContentType = contentType);
        }

        public IRestClient WithWebTransform(Action<HttpWebRequest> transform)
        {
            var client = new RestClient(_connectionSettings.Clone());
            client._connectionSettings.WebRequestTransform = transform;
            return client;
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
            _connectionSettings.AsDirectConnection(userDirectory, userId, port, certificateValidation,
                certificateCollection);
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

        private Borrowed<WebClient> GetClient()
        {
            return _clientPool.Borrow();
        }

        public string Get(string endpoint)
        {
            ValidateConfiguration();
            LogCall("GET", endpoint);
            using (var client = GetClient())
            {
                return LogReceive(client.It.DownloadString(BaseUri.Append(endpoint)));
            }
        }

        public Task<string> GetAsync(string endpoint)
        {
            ValidateConfiguration();
            LogCall("GET", endpoint);
            var client = GetClient();
            return LogReceive(client.It.DownloadStringTaskAsync(BaseUri.Append(endpoint))).ContinueWith(t => {client.Return(); return t.Result;});
        }

        private string PerformUploadStringAccess(string method, string endpoint, string body)
        {
            ValidateConfiguration();
            if (!_connectionSettings.HasCookie)
                CollectCookie();
            LogCall(method, endpoint);
            using (var client = GetClient())
            {
                return LogReceive(client.It.UploadString(BaseUri.Append(endpoint), method, body));
            }
        }

        private async Task<string> PerformUploadStringAccessAsync(string method, string endpoint, string body)
        {
            ValidateConfiguration();
            if (!_connectionSettings.HasCookie)
                await CollectCookieAsync();
            LogCall(method, endpoint);
            using (var client = GetClient())
                return await LogReceive(client.It.UploadStringTaskAsync(BaseUri.Append(endpoint), method, body));
        }

        public string Post(string endpoint, string body)
        {
            return PerformUploadStringAccess("POST", endpoint, body);
        }

        public Task<string> PostAsync(string endpoint, string body)
        {
            return PerformUploadStringAccessAsync("POST", endpoint, body);
        }

        public byte[] Post(string endpoint, byte[] body)
        {
            ValidateConfiguration();
            if (!_connectionSettings.HasCookie)
                CollectCookie();
            LogCall("POST", endpoint);
            using (var client = GetClient())
            {
                var data = client.It.UploadData(BaseUri.Append(endpoint), body);
                LogReceive("<binary data>");
                return data;
            }
        }

        public async Task<byte[]> PostAsync(string endpoint, byte[] body)
        {
            ValidateConfiguration();
            if (!_connectionSettings.HasCookie)
                CollectCookie();
            LogCall("POST", endpoint);
            using (var client = GetClient())
            {
                var data = await client.It.UploadDataTaskAsync(BaseUri.Append(endpoint), body);
                LogReceive("<binary data>");
                return data;
            }
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

        private void CollectCookie()
        {
            Get("/qrs/about");
        }

        private async Task CollectCookieAsync()
        {
            await GetAsync("/qrs/about");
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

    internal class SenseWebClient : WebClient
    {
        private readonly ConnectionSettings _connectionSettings;

        public SenseWebClient(ConnectionSettings settings)
        {
            _connectionSettings = settings;
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var xrfkey = CreateXrfKey();
            var request = (HttpWebRequest) base.GetWebRequest(AddXrefKey(address, xrfkey));
            request.ContentType = "application/json";
            request.Headers.Add("X-Qlik-Xrfkey", xrfkey);
            _connectionSettings.WebRequestTransform?.Invoke(request);

            var userHeaderValue = string.Format("UserDirectory={0};UserId={1}", _connectionSettings.UserDirectory,
                _connectionSettings.UserId);
            switch (_connectionSettings.ConnectionType)
            {
                case RestClient.ConnectionType.NtlmUserViaProxy:
                    request.UseDefaultCredentials = true;
                    request.AllowAutoRedirect = true;
                    request.UserAgent = "Windows";
                    break;
                case RestClient.ConnectionType.DirectConnection:
                    request.Headers.Add("X-Qlik-User", userHeaderValue);
                    foreach (var certificate in _connectionSettings.Certificates)
                    {
                        request.ClientCertificates.Add(certificate);
                    }

                    break;
                case RestClient.ConnectionType.StaticHeaderUserViaProxy:
                    request.Headers.Add(_connectionSettings.StaticHeaderName, _connectionSettings.UserId);
                    break;
            }

            request.CookieContainer = _connectionSettings.CookieJar;
            return request;
        }

        private static Uri AddXrefKey(Uri uri, string xrfkey)
        {
            var sb = new StringBuilder(uri.Query.TrimStart('?'));
            if (sb.Length > 0)
                sb.Append('&');
            sb.Append("xrfkey=" + xrfkey);
            var uriBuilder = new UriBuilder(uri) {Query = sb.ToString()};
            return uriBuilder.Uri;
        }

        private static string CreateXrfKey()
        {
            const string allowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
            var sb = new StringBuilder(16);
            using (var provider = new RNGCryptoServiceProvider())
            {
                var randomSequence = new byte[16];
                provider.GetBytes(randomSequence);
                foreach (var b in randomSequence)
                {
                    var character = allowedChars[b % allowedChars.Length];
                    sb.Append(character);
                }
            }

            return sb.ToString();
        }

    }
}