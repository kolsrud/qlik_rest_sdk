using System;
using System.IO;
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
        private Uri Uri { get; set; }
        private string _userDirectory;
        private string _userId;
        private X509Certificate2Collection _certificates;
        private readonly CookieContainer _cookieJar = new CookieContainer();

        public ConnectionType? CurrentConnectionType { get; private set; }

        public enum ConnectionType
        {
            NtlmUserViaProxy,
            DirectConnection
        }

        public RestClient(string uri)
        {
            Uri = new Uri(uri);
        }

        public void AsDirectConnection(int port = 4242, bool certificateValidation = true, X509Certificate2Collection certificateCollection = null)
        {
            AsDirectConnection(Environment.UserName, Environment.UserDomainName, port, certificateValidation, certificateCollection);
        }

        public void AsDirectConnection(string userDirectory, string userId, int port = 4242, bool certificateValidation = true, X509Certificate2Collection certificateCollection = null)
        {
            CurrentConnectionType = ConnectionType.DirectConnection;
            var uriBuilder = new UriBuilder(Uri);
            uriBuilder.Port = port;
            Uri = uriBuilder.Uri;
            _userId = userId;
            _userDirectory = userDirectory;
            _certificates = certificateCollection;
            if (!certificateValidation)
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
        }

        public void AsNtlmUserViaProxy()
        {
            UseDefaultCredentials = true;
            CurrentConnectionType = ConnectionType.NtlmUserViaProxy;
            _userId = Environment.UserName;
            _userDirectory = Environment.UserDomainName;
        }

        public void LoadCertificateFromDirectory(string path, SecureString certificatePassword = null)
        {
            var clientCertPath = Path.Combine(path, "client.pfx");
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);
            if (!File.Exists(clientCertPath)) throw new FileNotFoundException(clientCertPath);
            var certificate = certificatePassword == null ? new X509Certificate2(clientCertPath) : new X509Certificate2(clientCertPath, certificatePassword);
            _certificates = new X509Certificate2Collection(certificate);
        }

        public string Get(string endpoint)
        {
            ValidateConfiguration();
            return DownloadString(new Uri(Uri, endpoint));
        }

        public Task<string> GetAsync(string endpoint)
        {
            ValidateConfiguration();
            return DownloadStringTaskAsync(new Uri(Uri, endpoint));
        }

        public string Post(string endpoint, string body)
        {
            ValidateConfiguration();
            return UploadString(new Uri(Uri, endpoint), body);
        }

        public Task<string> PostAsync(string endpoint, string body)
        {
            ValidateConfiguration();
            return UploadStringTaskAsync(new Uri(Uri, endpoint), body);
        }

        public string Delete(string endpoint)
        {
            ValidateConfiguration();
            return UploadString(new Uri(Uri, endpoint), "DELETE", "");
        }

        public Task<string> DeleteAsync(string endpoint)
        {
            ValidateConfiguration();
            return UploadStringTaskAsync(new Uri(Uri, endpoint), "DELETE", "");
        }

        private void ValidateConfiguration()
        {
            if (CurrentConnectionType == null) throw new ConnectionNotConfiguredException();
            if (CurrentConnectionType == ConnectionType.DirectConnection && _certificates == null)
                throw new CertificatesNotLoadedException();
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var xrfkey = CreateXrfKey();
            var request = (HttpWebRequest)base.GetWebRequest(AddXrefKey(address, xrfkey));
            request.ContentType = "application/json";
            request.Headers.Add("X-Qlik-Xrfkey", xrfkey);
            var userHeaderValue = string.Format("UserDirectory={0};UserId={1}", _userDirectory, _userId);
            switch (CurrentConnectionType)
            {
                case ConnectionType.NtlmUserViaProxy:
                    request.Headers.Add("X-Qlik-User", userHeaderValue);
                    request.UserAgent = "Windows";
                    break;
                case ConnectionType.DirectConnection:
                    request.Headers.Add("X-Qlik-User", userHeaderValue);
                    foreach (var certificate in _certificates)
                    {
                        request.ClientCertificates.Add(certificate);
                    }
                    break;
            }
            request.CookieContainer = _cookieJar;
            return request;
        }

        private static Uri AddXrefKey(Uri uri, string xrfkey)
        {
            var sb = new StringBuilder(uri.Query);
            if (!string.IsNullOrEmpty(uri.Query))
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
}
