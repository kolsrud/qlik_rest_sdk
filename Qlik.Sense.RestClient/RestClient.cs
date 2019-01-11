using System;
using System.IO;
using System.Linq;
using System.Net;
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

        public string Url => _connectionSettings.BaseUri.AbsoluteUri;
        public Uri BaseUri => _connectionSettings.BaseUri;

        private readonly ConnectionSettings _connectionSettings;

	    public ConnectionType CurrentConnectionType => _connectionSettings.ConnectionType;

        private readonly Pool<WebClient> _clientPool;

        private RestClient(ConnectionSettings settings)
        {
            _connectionSettings = settings;
            _clientPool = new Pool<WebClient>(() => new SenseWebClient(_connectionSettings.Clone()));
        }

        public RestClient(string uri) : this(new ConnectionSettings(uri))
        {
	        _connectionSettings.AuthenticationFunc = CollectCookieAsync;
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
            _connectionSettings.AsDirectConnection(port, certificateValidation, certificateCollection);
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
	        if (!Authenticate())
		        throw new AuthenticationException("Authentication failed.");
            LogCall("GET", endpoint);
            using (var client = GetClient())
            {
                return LogReceive(client.It.DownloadString(BaseUri.Append(endpoint)));
            }
        }

        public async Task<string> GetAsync(string endpoint)
        {
            ValidateConfiguration();
	        if (!await AuthenticateAsync())
		        throw new AuthenticationException("Authentication failed.");
            LogCall("GET", endpoint);
	        using (var client = GetClient())
	        {
		        return await LogReceive(client.It.DownloadStringTaskAsync(BaseUri.Append(endpoint)));
	        }
        }

        private string PerformUploadStringAccess(string method, string endpoint, string body)
        {
            ValidateConfiguration();
	        if (!Authenticate())
		        throw new AuthenticationException("Authentication failed.");
            LogCall(method, endpoint);
            using (var client = GetClient())
            {
                return LogReceive(client.It.UploadString(BaseUri.Append(endpoint), method, body));
            }
        }

        private async Task<string> PerformUploadStringAccessAsync(string method, string endpoint, string body)
        {
            ValidateConfiguration();
            if (!await AuthenticateAsync())
	            throw new AuthenticationException("Authentication failed.");
			LogCall(method, endpoint);
	        using (var client = GetClient())
	        {
		        return await LogReceive(client.It.UploadStringTaskAsync(BaseUri.Append(endpoint), method, body));
	        }
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
            ValidateConfiguration();
            if (!Authenticate())
                throw new AuthenticationException("Authentication failed.");
            LogCall("POST", endpoint);
            using (var client = GetClient())
            {
                var data = client.It.UploadData(BaseUri.Append(endpoint), body);
                var rsp = System.Text.Encoding.Default.GetString(data);
                LogReceive(rsp);
                return rsp;
            }
        }

        public async Task<string> PostAsync(string endpoint, byte[] body)
        {
            ValidateConfiguration();
	        if (!await AuthenticateAsync())
		        throw new AuthenticationException("Authentication failed.");
            LogCall("POST", endpoint);
            using (var client = GetClient())
            {
                var data = await client.It.UploadDataTaskAsync(BaseUri.Append(endpoint), body);
                var rsp = System.Text.Encoding.Default.GetString(data);
                LogReceive(rsp);
                return rsp;
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

        private async Task CollectCookieAsync()
        {
			DebugConsole?.Log($"Authenticating (calling GET /qrs/about)");
	        using (var client = GetClient())
	        {
		        await LogReceive(client.It.DownloadStringTaskAsync(BaseUri.Append("/qrs/about")));
		        DebugConsole?.Log($"Authentication complete.");
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
}