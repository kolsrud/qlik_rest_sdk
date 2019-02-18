using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Qlik.Sense.RestClient
{
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
	        // ReSharper disable once PossibleNullReferenceException
            request.ContentType = "application/json";
            request.Headers.Add("X-Qlik-Xrfkey", xrfkey);
            _connectionSettings.WebRequestTransform?.Invoke(request);

	        switch (_connectionSettings.ConnectionType)
	        {
		        case ConnectionType.NtlmUserViaProxy:
			        if (_connectionSettings.CustomCredential == null)
				        request.UseDefaultCredentials = true;
			        else
			        {
				        request.UseDefaultCredentials = false;
				        request.Headers[HttpRequestHeader.Authorization] = "ntlm";
				        request.Credentials = _connectionSettings.CustomCredential;
			        }
			        request.AllowAutoRedirect = true;
			        request.UserAgent = "Windows";
			        break;
		        case ConnectionType.DirectConnection:
			        var userHeaderValue = string.Format("UserDirectory={0};UserId={1}",
				        _connectionSettings.UserDirectory,
				        _connectionSettings.UserId);
			        request.Headers.Add("X-Qlik-User", userHeaderValue);
			        foreach (var certificate in _connectionSettings.Certificates)
			        {
				        request.ClientCertificates.Add(certificate);
			        }

			        break;
		        case ConnectionType.StaticHeaderUserViaProxy:
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