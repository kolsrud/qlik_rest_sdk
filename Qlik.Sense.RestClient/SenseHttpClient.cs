using System;
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
            if (_connectionSettings.CertificateValidation == false)
                DeactivateCertificateValidation();

            var client = new HttpClient(_clientHandler);
            foreach (var header in _connectionSettings.CustomHeaders)
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
            _xrfkey = CreateXrfKey();
            client.DefaultRequestHeaders.Add("X-Qlik-Xrfkey", _xrfkey);
            _connectionSettings.WebRequestTransform?.Invoke(client);
            client.Timeout = _connectionSettings.Timeout;

            return client;
        }

        public async Task<string> GetStringAsync(Uri uri)
        {
            var client = _client.Value;
            var rsp = await client.GetAsync(AddXrefKey(uri, _xrfkey)).ConfigureAwait(false);
            if (rsp.IsSuccessStatusCode)
            {
                return await rsp.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            
            throw new HttpRequestException((int) rsp.StatusCode + ": " + rsp.ReasonPhrase);
        }

        public async Task<string> PostStringAsync(Uri uri, string body)
        {
            var client = _client.Value;
            var rbody = new StringContent(body, Encoding.ASCII, _connectionSettings.ContentType);
            rbody.Headers.ContentType = new MediaTypeWithQualityHeaderValue(_connectionSettings.ContentType);
            var rsp = await client.PostAsync(AddXrefKey(uri, _xrfkey), rbody).ConfigureAwait(false);
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
            var rsp = await client.PutAsync(AddXrefKey(uri, _xrfkey), rbody).ConfigureAwait(false);
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
            var rsp = await client.PostAsync(AddXrefKey(uri, _xrfkey), rbody).ConfigureAwait(false);
            if (rsp.IsSuccessStatusCode)
            {
                return await rsp.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            throw new HttpRequestException((int)rsp.StatusCode + ": " + rsp.ReasonPhrase);
        }

        public async Task<string> DeleteAsync(Uri uri)
        {
            var client = _client.Value;
            var rsp = await client.DeleteAsync(AddXrefKey(uri, _xrfkey)).ConfigureAwait(false);
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
    }
}