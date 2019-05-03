using System;
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
        private readonly HttpClientHandler _clientHandler;
        private string _xrfkey;

        private Lazy<HttpClient> _client;

        internal SenseHttpClient(ConnectionSettings connectionSettings, HttpClientHandler clientHandler)
        {
            _connectionSettings = connectionSettings;
            _clientHandler = clientHandler;
            _client = new Lazy<HttpClient>(InitializeClient);
        }

        private HttpClient InitializeClient()
        {
            _xrfkey = CreateXrfKey();
            var client = new HttpClient(_clientHandler);
            foreach (var header in _connectionSettings.CustomHeaders)
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
            client.DefaultRequestHeaders.Add("X-Qlik-Xrfkey", _xrfkey);
            _connectionSettings.WebRequestTransform?.Invoke(client);
            return client;
        }

        public async Task<string> GetStringAsync(Uri uri)
        {
            var client = _client.Value;
            var rsp = await client.GetAsync(AddXrefKey(uri, _xrfkey));
            return await rsp.Content.ReadAsStringAsync();
        }

        public async Task<string> PostStringAync(Uri uri, string body)
        {
            var client = _client.Value;
            var rbody = new StringContent(body, Encoding.ASCII, _connectionSettings.ContentType);
            rbody.Headers.ContentType = new MediaTypeWithQualityHeaderValue(_connectionSettings.ContentType);
            var rsp = await client.PostAsync(AddXrefKey(uri, _xrfkey), rbody);
            return await rsp.Content.ReadAsStringAsync();
        }

        public async Task<string> PutStringAync(Uri uri, string body)
        {
            var client = _client.Value;
            var rbody = new StringContent(body, Encoding.ASCII, _connectionSettings.ContentType);
            rbody.Headers.ContentType = new MediaTypeWithQualityHeaderValue(_connectionSettings.ContentType);
            var rsp = await client.PutAsync(AddXrefKey(uri, _xrfkey), rbody);
            return await rsp.Content.ReadAsStringAsync();
        }

        public async Task<string> PostDataAync(Uri uri, byte[] body)
        {
            var client = _client.Value;
            var rbody = new ByteArrayContent(body);
            rbody.Headers.ContentType = new MediaTypeWithQualityHeaderValue(_connectionSettings.ContentType);
            var rsp = await client.PostAsync(AddXrefKey(uri, _xrfkey), rbody);
            return await rsp.Content.ReadAsStringAsync();
        }

        public async Task<string> DeleteAync(Uri uri)
        {
            var client = _client.Value;
            var rsp = await client.DeleteAsync(AddXrefKey(uri, _xrfkey));
            return await rsp.Content.ReadAsStringAsync();
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