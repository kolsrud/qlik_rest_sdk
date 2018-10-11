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
    public class RestClient : WebClient, IRestClient
    {
        public string Url => BaseUri.AbsoluteUri;
        private Uri BaseUri { get; set; }
		private string _userDirectory;
        private string _userId;
	    private string _staticHeaderName;
        private X509Certificate2Collection _certificates;
        private readonly CookieContainer _cookieJar = new CookieContainer();
        public Action<HttpWebRequest> WebRequestTransform { get; set; }

        public ConnectionType? CurrentConnectionType { get; private set; }

        public enum ConnectionType
        {
            NtlmUserViaProxy,
            StaticHeaderUserViaProxy,
            DirectConnection
        }

        public RestClient(string uri)
        {
            BaseUri = new Uri(uri);
        }

        public void AsDirectConnection(int port = 4242, bool certificateValidation = true, X509Certificate2Collection certificateCollection = null)
        {
            AsDirectConnection(Environment.UserName, Environment.UserDomainName, port, certificateValidation, certificateCollection);
        }

        public void AsDirectConnection(string userDirectory, string userId, int port = 4242, bool certificateValidation = true, X509Certificate2Collection certificateCollection = null)
        {
            CurrentConnectionType = ConnectionType.DirectConnection;
            var uriBuilder = new UriBuilder(BaseUri) {Port = port};
            BaseUri = uriBuilder.Uri;
            _userId = userId;
            _userDirectory = userDirectory;
            _certificates = certificateCollection;
            if (!certificateValidation)
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
        }

        public void AsNtlmUserViaProxy(bool certificateValidation = true)
        {
            UseDefaultCredentials = true;
            CurrentConnectionType = ConnectionType.NtlmUserViaProxy;
            _userId = Environment.UserName;
            _userDirectory = Environment.UserDomainName;
            if (!certificateValidation)
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
        }

        public void AsStaticHeaderUserViaProxy(string userId, string headerName, bool certificateValidation = true)
        {
            CurrentConnectionType = ConnectionType.StaticHeaderUserViaProxy;
            _userId = userId;
            _userDirectory = Environment.UserDomainName;
	        _staticHeaderName = headerName;
            if (!certificateValidation)
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
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

        public static X509Certificate2Collection LoadCertificateFromDirectory(string path, SecureString certificatePassword = null)
        {
            var clientCertPath = Path.Combine(path, "client.pfx");
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);
            if (!File.Exists(clientCertPath)) throw new FileNotFoundException(clientCertPath);
            var certificate = certificatePassword == null ? new X509Certificate2(clientCertPath) : new X509Certificate2(clientCertPath, certificatePassword);
            return new X509Certificate2Collection(certificate);
        }

        public string Get(string endpoint)
        {
            ValidateConfiguration();
            return DownloadString(BaseUri.Append(endpoint));
        }

        public Task<string> GetAsync(string endpoint)
        {
            ValidateConfiguration();
            return DownloadStringTaskAsync(Url + endpoint);
        }

        public string Post(string endpoint, string body)
        {
            ValidateConfiguration();
            if (_cookieJar.Count == 0)
                CollectCookie();
            return UploadString(BaseUri.Append(endpoint), body);
        }

        public async Task<string> PostAsync(string endpoint, string body)
        {
            ValidateConfiguration();
            if (_cookieJar.Count == 0)
                await CollectCookieAsync();
            return await UploadStringTaskAsync(BaseUri.Append(endpoint), body);
        }

        public byte[] Post(string endpoint, byte[] body)
        {
            ValidateConfiguration();
            if (_cookieJar.Count == 0)
                CollectCookie();
            return UploadData(BaseUri.Append(endpoint), body);
        }

        public async Task<byte[]> PostAsync(string endpoint, byte[] body)
        {
            ValidateConfiguration();
            if (_cookieJar.Count == 0)
                CollectCookie();
            return await UploadDataTaskAsync(BaseUri.Append(endpoint), body);
        }

        public string Delete(string endpoint)
        {
            ValidateConfiguration();
            if (_cookieJar.Count == 0)
                CollectCookie();
            return UploadString(BaseUri.Append(endpoint), "DELETE", "");
        }

        public async Task<string> DeleteAsync(string endpoint)
        {
            ValidateConfiguration();
            if (_cookieJar.Count == 0)
                await CollectCookieAsync();
            return await UploadStringTaskAsync(BaseUri.Append(endpoint), "DELETE", "");
        }

        private void ValidateConfiguration()
        {
            if (CurrentConnectionType == null) throw new ConnectionNotConfiguredException();
            if (CurrentConnectionType == ConnectionType.DirectConnection && _certificates == null)
                throw new CertificatesNotLoadedException();
        }

        private void CollectCookie()
        {
            Get("/qrs/about");
        }

        private async Task CollectCookieAsync()
        {
            await GetAsync("/qrs/about");
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var xrfkey = CreateXrfKey();
            var request = (HttpWebRequest)base.GetWebRequest(AddXrefKey(address, xrfkey));
            request.ContentType = "application/json";
            request.Headers.Add("X-Qlik-Xrfkey", xrfkey);
            WebRequestTransform?.Invoke(request);

            var userHeaderValue = string.Format("UserDirectory={0};UserId={1}", _userDirectory, _userId);
            switch (CurrentConnectionType)
            {
                case ConnectionType.NtlmUserViaProxy:
                    request.UseDefaultCredentials = true;
                    request.AllowAutoRedirect = true;
                    request.UserAgent = "Windows";
                    break;
                case ConnectionType.DirectConnection:
	                request.Headers.Add("X-Qlik-User", userHeaderValue);
                    foreach (var certificate in _certificates)
                    {
                        request.ClientCertificates.Add(certificate);
                    }
                    break;
				case ConnectionType.StaticHeaderUserViaProxy:
					request.Headers.Add(_staticHeaderName, _userId);
					break;
            }
            request.CookieContainer = _cookieJar;
            return request;
        }

        private static Uri AddXrefKey(Uri uri, string xrfkey)
        {
            var sb = new StringBuilder(uri.Query.TrimStart('?'));
            if (sb.Length > 0)
                sb.Append('&');
            sb.Append("xrfkey=" + xrfkey);
            var uriBuilder = new UriBuilder(uri) { Query = sb.ToString() };
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
            return new Uri(paths.Aggregate(uri.AbsoluteUri, (current, path) => string.Format("{0}/{1}", current.TrimEnd('/'), path.TrimStart('/'))));
        }
    }

    /// <summary>
    /// Highly experimental...
    /// </summary>
    public class WebRequestSetting : IDisposable
    {
        private readonly IRestClient _client;
        private readonly Action<HttpWebRequest> _oldTransform;

        public WebRequestSetting(IRestClient client, Action<HttpWebRequest> newTransform)
        {
            _client = client;
            _oldTransform = client.WebRequestTransform;
            client.WebRequestTransform = newTransform;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool _disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                _client.WebRequestTransform = _oldTransform;
            }
        }
    }
}