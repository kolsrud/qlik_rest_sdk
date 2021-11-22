using System;
using System.IO;
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
        private readonly ConnectionSettings _connectionSettings;
#if (NETCOREAPP2_1)
        private readonly HttpClientHandler _clientHandler;
#else
        private readonly WebRequestHandler _clientHandler;
#endif
        private string _xrfkey;

        private readonly Lazy<HttpClient> _client;

        internal SenseHttpClient(ConnectionSettings connectionSettings)
        {
            _connectionSettings = connectionSettings;
#if (NETCOREAPP2_1)
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
#if (NETCOREAPP2_1)
            _clientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
#else
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
#endif
        }

        private HttpClient InitializeClient()
        {
#if (NET452)
            ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
#endif
            if (_connectionSettings.CertificateValidation == false)
                DeactivateCertificateValidation();

            if (_connectionSettings.ConnectionType == ConnectionType.JwtTokenViaQcs)
            {
                _clientHandler.AllowAutoRedirect = false;
            }

            var client = new HttpClient(_clientHandler);
            foreach (var header in _connectionSettings.CustomHeaders)
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }

            if (UseXrfKey)
            {
                _xrfkey = _connectionSettings.Xrfkey ?? CreateXrfKey();
                client.DefaultRequestHeaders.Add("X-Qlik-Xrfkey", _xrfkey);
            }

            client.Timeout = _connectionSettings.Timeout;

            return client;
        }

        private bool UseXrfKey => _connectionSettings.ConnectionType != ConnectionType.JwtTokenViaQcs;

        public Task<HttpResponseMessage> GetHttpAsync(Uri uri, bool throwOnFailure = true)
        {
            return GetHttpAsync(uri, throwOnFailure, HttpCompletionOption.ResponseContentRead);
        }

        private async Task<HttpResponseMessage> GetHttpAsync(Uri uri, bool throwOnFailure, HttpCompletionOption completionOption)
        {
            var client = _client.Value;
            var rsp = await client.GetAsync(AddXrefKey(UseXrfKey, uri, _xrfkey), completionOption).ConfigureAwait(false);
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
            catch {}

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
            return PostHttpAsync(uri, new StringContent(body, Encoding.ASCII, _connectionSettings.ContentType), throwOnFailure);
        }

        private async Task<HttpResponseMessage> PostHttpAsync(Uri uri, HttpContent body, bool throwOnFailure = true)
        {
            var client = _client.Value;
            body.Headers.ContentType = new MediaTypeWithQualityHeaderValue(_connectionSettings.ContentType);
            var rsp = await client.PostAsync(AddXrefKey(UseXrfKey, uri, _xrfkey), body).ConfigureAwait(false);

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
        
        public async Task<string> PostStringAsync(Uri uri, string body)
        {
            var client = _client.Value;
            var rbody = new StringContent(body, Encoding.ASCII, _connectionSettings.ContentType);
            rbody.Headers.ContentType = new MediaTypeWithQualityHeaderValue(_connectionSettings.ContentType);
            var rsp = await client.PostAsync(AddXrefKey(UseXrfKey, uri, _xrfkey), rbody).ConfigureAwait(false);
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