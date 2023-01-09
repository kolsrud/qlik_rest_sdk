using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Qlik.Sense.RestClient
{
    public class SenseHttpClient
    {
        internal const string CSRF_TOKEN_ID = "qlik-csrf-token";
        private readonly string _userAgent;

        private readonly ConnectionSettings _connectionSettings;
#if (NETCOREAPP)
        private readonly HttpClientHandler _clientHandler;
#else
        private readonly WebRequestHandler _clientHandler;
#endif
        private string _xrfkey;

        private readonly Lazy<HttpClient> _client;

        internal SenseHttpClient(ConnectionSettings connectionSettings)
        {
            _userAgent = $"{SystemConstants.LIBRARY_IDENTIFIER}/{SystemConstants.LIBRARY_VERSION}";
            _connectionSettings = connectionSettings;
#if (NETCOREAPP)
            _clientHandler = new HttpClientHandler();
#else
            _clientHandler = new WebRequestHandler();
#endif
            _clientHandler.CookieContainer = _connectionSettings.CookieJar;
            _clientHandler.Proxy = _connectionSettings.Proxy;
            if (_connectionSettings.Certificates != null)
                _clientHandler.ClientCertificates.AddRange(_connectionSettings.Certificates);
            if (connectionSettings.CustomCredential != null)
            {
                _clientHandler.Credentials = _connectionSettings.CustomCredential;
            }

            _client = new Lazy<HttpClient>(InitializeClient);
        }

        private void DeactivateCertificateValidation()
        {
#if (NETCOREAPP)
            _clientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
#else
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
#endif
        }

        private HttpClient InitializeClient()
        {
#if (NET452 || NET462)
            ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
#endif
            if (_connectionSettings.CertificateValidation == false)
                DeactivateCertificateValidation();

            if (_connectionSettings.ConnectionType == ConnectionType.ApiKeyViaQcs
                || _connectionSettings.ConnectionType == ConnectionType.ClientCredentialsViaQcs
                )
            {
                _clientHandler.AllowAutoRedirect = false;
            }

            var client = new HttpClient(_clientHandler);
            foreach (var header in _connectionSettings.CustomHeaders)
            {
                client.DefaultRequestHeaders.Add(header.Key.ToLower(), header.Value);
            }
            if (!string.IsNullOrWhiteSpace(_connectionSettings.CustomUserAgent))
                client.DefaultRequestHeaders.Add("user-agent", _connectionSettings.CustomUserAgent);
            client.DefaultRequestHeaders.Add("user-agent", _userAgent);
            
            if (UseXrfKey)
            {
                _xrfkey = _connectionSettings.Xrfkey ?? CreateXrfKey();
                client.DefaultRequestHeaders.Add("X-Qlik-Xrfkey", _xrfkey);
            }

            client.Timeout = _connectionSettings.Timeout;

            return client;
        }

        internal void AddDefaultHeader(string name, string value)
        {
            var client = _client.Value;
            client.DefaultRequestHeaders.Add(name, value);
        }

        private bool UseXrfKey => !new[] { ConnectionType.JwtTokenViaQcs, ConnectionType.ApiKeyViaQcs, ConnectionType.ClientCredentialsViaQcs }.Contains(_connectionSettings.ConnectionType);

        public Task<HttpResponseMessage> GetHttpAsync(Uri uri, bool throwOnFailure = true)
        {
            return GetHttpAsync(uri, throwOnFailure, HttpCompletionOption.ResponseContentRead);
        }

        private async Task<HttpResponseMessage> GetHttpAsync(Uri uri, bool throwOnFailure, HttpCompletionOption completionOption)
        {
            var client = _client.Value;
            var rsp = await client.GetAsync(AddDefaultArguments(uri, true), completionOption).ConfigureAwait(false);
            if (rsp.StatusCode == HttpStatusCode.MovedPermanently)
            {
                rsp = await client.GetAsync(rsp.Headers.Location).ConfigureAwait(false);
            }

            if (rsp.IsSuccessStatusCode || !throwOnFailure)
            {
                return rsp;
            }

            var message = $"{(int)rsp.StatusCode} ({rsp.StatusCode}): {rsp.ReasonPhrase}";
            try
            {
                var reason = await rsp.Content.ReadAsStringAsync().ConfigureAwait(false);
                message += ", " + reason;
            }
            catch { }

            throw new HttpRequestException(message);
        }

        public async Task<string> GetStringAsync(Uri uri)
        {
            var rsp = await GetHttpAsync(uri).ConfigureAwait(false);
            return await rsp.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        public async Task<byte[]> GetBytesAsync(Uri uri)
        {
            var rsp = await GetHttpAsync(uri).ConfigureAwait(false);
            return await rsp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        }

        public async Task<Stream> GetStreamAsync(Uri uri)
        {
            var rsp = await GetHttpAsync(uri, true, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            return await rsp.Content.ReadAsStreamAsync().ConfigureAwait(false);
        }

        public Task<HttpResponseMessage> PostHttpAsync(Uri uri, string body, bool throwOnFailure = true)
        {
            return PostHttpAsync(uri, new StringContent(body, Encoding.UTF8, _connectionSettings.ContentType), throwOnFailure);
        }

        private async Task<HttpResponseMessage> PostHttpAsync(Uri uri, HttpContent body, bool throwOnFailure = true)
        {
            var client = _client.Value;
            body.Headers.ContentType = new MediaTypeWithQualityHeaderValue(_connectionSettings.ContentType);
            var rsp = await client.PostAsync(AddDefaultArguments(uri), body).ConfigureAwait(false);

            if (rsp.IsSuccessStatusCode || !throwOnFailure)
            {
                return rsp;
            }

            var message = (int)rsp.StatusCode + ": " + rsp.ReasonPhrase;
            try
            {
                var reason = await rsp.Content.ReadAsStringAsync().ConfigureAwait(false);
                message += ", " + reason;
            }
            catch { }

            throw new HttpRequestException(message);
        }

        private Uri AddDefaultArguments(Uri uri, bool isGetRequest = false)
        {
            if (UseXrfKey)
                return AddXrefKey(true, uri, _xrfkey);

            var argsToAdd = string.Join("&", _connectionSettings.DefaultArguments.Select(kv => kv.Key + '=' + kv.Value));
            var uriBuilder = new UriBuilder(uri);
            if (!string.IsNullOrWhiteSpace(argsToAdd))
            {
                if (string.IsNullOrEmpty(uriBuilder.Query))
                    uriBuilder.Query = argsToAdd;
                else
                    uriBuilder.Query += "&" + argsToAdd;
            }
            return uriBuilder.Uri;
        }

        public async Task<string> PostStringAsync(Uri uri, string body)
        {
            var client = _client.Value;
            var rbody = new StringContent(body, Encoding.ASCII, _connectionSettings.ContentType);
            rbody.Headers.ContentType = new MediaTypeWithQualityHeaderValue(_connectionSettings.ContentType);
            var rsp = await client.PostAsync(AddDefaultArguments(uri), rbody).ConfigureAwait(false);
            if (rsp.IsSuccessStatusCode)
            {
                return await rsp.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            throw new HttpRequestException((int)rsp.StatusCode + ": " + rsp.ReasonPhrase);
        }

        public async Task<string> PutStringAsync(Uri uri, string body)
        {
            var client = _client.Value;
            var rbody = new StringContent(body, Encoding.ASCII, _connectionSettings.ContentType);
            rbody.Headers.ContentType = new MediaTypeWithQualityHeaderValue(_connectionSettings.ContentType);
            var rsp = await client.PutAsync(AddXrefKey(UseXrfKey, uri, _xrfkey), rbody).ConfigureAwait(false);
            if (rsp.IsSuccessStatusCode)
            {
                return await rsp.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            throw new HttpRequestException((int)rsp.StatusCode + ": " + rsp.ReasonPhrase);
        }

        public async Task<string> PostDataAsync(Uri uri, byte[] body)
        {
            var client = _client.Value;
            var rbody = new ByteArrayContent(body);
            rbody.Headers.ContentType = new MediaTypeWithQualityHeaderValue(_connectionSettings.ContentType);
            var rsp = await client.PostAsync(AddXrefKey(UseXrfKey, uri, _xrfkey), rbody).ConfigureAwait(false);
            if (rsp.IsSuccessStatusCode)
            {
                return await rsp.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            throw new HttpRequestException((int)rsp.StatusCode + ": " + rsp.ReasonPhrase);
        }

        public async Task<string> PostHttpContentAsync(Uri uri, HttpContent content)
        {
            var client = _client.Value;
            var rsp = await client.PostAsync(AddXrefKey(UseXrfKey, uri, _xrfkey), content).ConfigureAwait(false);
            if (rsp.IsSuccessStatusCode)
            {
                return await rsp.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            throw new HttpRequestException((int)rsp.StatusCode + ": " + rsp.ReasonPhrase);
        }

        public async Task<string> PutHttpContentAsync(Uri uri, HttpContent content)
        {
            var client = _client.Value;
            var rsp = await client.PutAsync(AddXrefKey(UseXrfKey, uri, _xrfkey), content).ConfigureAwait(false);
            if (rsp.IsSuccessStatusCode)
            {
                return await rsp.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            throw new HttpRequestException((int)rsp.StatusCode + ": " + rsp.ReasonPhrase);
        }

        public async Task<string> DeleteAsync(Uri uri)
        {
            var client = _client.Value;
            var rsp = await client.DeleteAsync(AddXrefKey(UseXrfKey, uri, _xrfkey)).ConfigureAwait(false);
            if (rsp.IsSuccessStatusCode)
            {
                return await rsp.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            throw new HttpRequestException((int)rsp.StatusCode + ": " + rsp.ReasonPhrase);
        }

        public HttpClient GetClient()
        {
            return _client.Value;
        }

        private static Uri AddXrefKey(bool addXrfKey, Uri uri, string xrfkey)
        {
            if (!addXrfKey)
                return uri;

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
    }
}