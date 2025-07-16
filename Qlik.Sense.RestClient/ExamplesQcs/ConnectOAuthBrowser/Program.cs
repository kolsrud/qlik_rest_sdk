using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Qlik.OAuthManager;
using Qlik.Sense.RestClient;

namespace ConnectOAuthBrowser
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var tenantUrl = "<url>";
            var clientId = "<clientId>";
            var redirectUri = "<redirectUri>"; // Must be in sync with the OAuth client configuration in the tenant. Example: "http://localhost:8123"

            // Fetch access token using the library Qlik.OAuthManager (available through NuGet: https://www.nuget.org/packages/Qlik.OAuthManager)
            var oauthManager = new OAuthManager(tenantUrl, clientId);
            await oauthManager.AuthorizeInBrowser("user_default offline_access", redirectUri, Browser.Default);
            var accessToken = await oauthManager.RequestNewAccessToken();

            // Use access token just like an API key.
            var restClient = new RestClientQcs(tenantUrl);
            restClient.AsApiKey(accessToken);

            Console.WriteLine(restClient.Get<JToken>("/api/v1/users/me"));
        }
    }
}
