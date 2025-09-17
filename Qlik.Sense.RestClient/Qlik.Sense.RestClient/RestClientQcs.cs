using System.Security.Authentication;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json.Linq;

namespace Qlik.Sense.RestClient
{
    public interface IRestClientQcs : IRestClientGeneric
    {
        QcsSessionInfo QcsSessionInfo { get; }

        void AsApiKey(string apiKey);
        void AsJwt(string jwt);
        void AsExistingSessionViaQcs(QcsSessionInfo sessionInfo);

        IRestClientQcs WithContentType(string contentType);

        /// <summary>
        /// Utility method to fetch resources using the standard paging mechanism used in QCS. The endpoint is called
        /// repeatedly as long as there is a <c>next</c> property in the response.
        /// </summary>
        /// <param name="endpoint">The initial endpoint used for page iteration.</param>
        /// <returns>An <c>IEnumerable</c> with the set of resources fetched.</returns>
        IEnumerable<JObject> FetchAll(string endpoint);

		/// <summary>
		/// Utility method to fetch all resources of a specific type. Method uses the <c>items</c> endpoint and calls
		/// that endpoint repeatedly as long as there is a <c>next</c> property in the response.
		/// </summary>
		/// <example>var apps = FetchAllItems("app")</example>
		/// <param name="resourceType">The type of the resource to fetch.</param>
		/// <param name="pageSize">Number of items to fetch per REST call.  Must be in range 1-100.</param>
		/// <returns>An <c>IEnumerable</c> with the set of resources fetched.</returns>
		IEnumerable<JObject> FetchAllItems(string resourceType, int pageSize = 10);

		/// <summary>
		/// Utility method to fetch resources using the standard paging mechanism used in QCS. The endpoint is called
		/// repeatedly as long as there is a <c>next</c> property in the response.
		/// </summary>
		/// <param name="endpoint">The initial endpoint used for page iteration.</param>
		/// <returns>An <c>IEnumerable</c> with the set of resources fetched.</returns>
        Task<IEnumerable<JObject>> FetchAllAsync(string endpoint);

		/// <summary>
		/// Utility method to fetch all resources of a specific type. Method uses the <c>items</c> endpoint and calls
		/// that endpoint repeatedly as long as there is a <c>next</c> property in the response.
		/// </summary>
		/// <example>var apps = FetchAllItems("app")</example>
		/// <param name="resourceType">The type of the resource to fetch.</param>
		/// <param name="pageSize">Number of items to fetch per REST call. Must be in range 1-100.</param>
		/// <returns>An <c>IEnumerable</c> with the set of resources fetched.</returns>
		Task<IEnumerable<JObject>> FetchAllItemsAsync(string resourceType, int pageSize = 10);
	}

	public class RestClientQcs : RestClientGeneric, IRestClientQcs
    {
        private RestClientQcs(RestClientQcs source) : base(source)
        {
        }

        public RestClientQcs(Uri uri) : base(uri)
        {
            _connectionSettings.IsQcs = true;
        }

        public RestClientQcs(string uri) : this(new Uri(uri))
        {
        }

        public void AsApiKey(string apiKey)
        {
            _connectionType = ConnectionType.ApiKeyViaQcs;
            _connectionSettings.AllowAutoRedirect = false;
            AddBearerToken(apiKey);
            _connectionSettings.IsAuthenticated = true;
        }

        public void AsJwt(string jwt)
        {
            _connectionType = ConnectionType.JwtTokenViaQcs;
            AddBearerToken(jwt);
            _connectionSettings.IsAuthenticated = false;
            _connectionSettings.IsQcs = true;
            _connectionSettings.AuthenticationFunc = CollectCookieJwtViaQcsAsync;
        }

        public void AsExistingSessionViaQcs(QcsSessionInfo sessionInfo)
        {
            _connectionType = ConnectionType.ExistingSessionViaQcs;
            _connectionSettings.IsQcs = true;
            _connectionSettings.CookieJar.Add(BaseUri, new Cookie("eas.sid", sessionInfo.EasSid));
            _connectionSettings.CookieJar.Add(BaseUri, new Cookie("eas.sid.sig", sessionInfo.EasSidSig));
            CustomHeaders[SenseHttpClient.CSRF_TOKEN_ID] = sessionInfo.SessionToken;
        }

        public IRestClientQcs WithContentType(string contentType)
        {
            var client = new RestClientQcs(this);
            client._connectionSettings.ContentType = contentType;
            return client;
        }

		/// <inheritdoc />
		public IEnumerable<JObject> FetchAllItems(string resourceType, int pageSize = 10)
		{
			if (pageSize < 1 || pageSize > 100)
				throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be in the range 1-100.");

	        return FetchAll($"/api/v1/items?resourceType={resourceType}&limit={pageSize}");
        }

        /// <inheritdoc />
		public Task<IEnumerable<JObject>> FetchAllItemsAsync(string resourceType, int pageSize = 10)
		{
			if (pageSize < 1 || pageSize > 100)
				throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be in the range 1-100.");

			return FetchAllAsync($"/api/v1/items?resourceType={resourceType}&limit={pageSize}");
		}

		/// <inheritdoc />
		public IEnumerable<JObject> FetchAll(string endpoint)
        {
	        var next = endpoint;
	        // Continue fetching data as long as there is a "next" reference.
	        while (next != null)
	        {
		        var spacesData = Get<JObject>(next);
		        foreach (var obj in spacesData["data"].Values<JObject>())
		        {
			        yield return obj;
		        }
		        next = spacesData["links"]["next"]?["href"]?.Value<string>()?.Substring(Url.Length);
	        }
        }

		/// <inheritdoc />
		public async Task<IEnumerable<JObject>> FetchAllAsync(string endpoint)
		{
			var data = new List<JObject>();
			var next = endpoint;
			// Continue fetching data as long as there is a "next" reference.
			while (next != null)
			{
				var spacesData = await GetAsync<JObject>(next).ConfigureAwait(false);
				data.AddRange(spacesData["data"].Values<JObject>());
				next = spacesData["links"]["next"]?["href"]?.Value<string>()?.Substring(Url.Length);
			}

			return data;
		}
        
		private void AddBearerToken(string token)
        {
            CustomHeaders.Add("Authorization", "Bearer " + token);
        }

        private async Task CollectCookieJwtViaQcsAsync()
        {
            RestClientDebugConsole?.Log($"Authenticating (calling POST /login/jwt-session)");
            var client = GetClient();
            await LogReceive(client.PostStringAsync(BaseUri.Append("/login/jwt-session"), "")).ConfigureAwait(false);

            var csrfToken = _connectionSettings.GetCookie("_csrfToken").Value;
            if (csrfToken == null)
            {
                throw new AuthenticationException("Call to /login/jwt-session did not return a csrf token cookie.");
            }

            client.AddDefaultHeader(SenseHttpClient.CSRF_TOKEN_ID, csrfToken);
            RestClientDebugConsole?.Log($"Authentication complete.");
        }
    }
}