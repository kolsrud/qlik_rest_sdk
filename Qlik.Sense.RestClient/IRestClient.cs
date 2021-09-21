using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Qlik.Sense.RestClient.Qrs;

namespace Qlik.Sense.RestClient
{
    public interface IRestClient
    {
        User User { get; }
        string Url { get; }
        string UserId { get; }
        string UserDirectory { get; }

        bool Authenticate();

        IRestClient ConnectAsQmc();
        IRestClient ConnectAsHub();
        IRestClient WithXrfkey(string xrfkey);
        IRestClient WithContentType(string contentType);

        string Get(string endpoint);
        T Get<T>(string endpoint);
        Task<string> GetAsync(string endpoint);
        Task<T> GetAsync<T>(string endpoint);
        byte[] GetBytes(string endpoint);
        Task<byte[]> GetBytesAsync(string endpoint);
        Stream GetStream(string endpoint);
        Task<Stream> GetStreamAsync(string endpoint);
        Task<HttpResponseMessage> GetHttpAsync(string endpoint, bool throwOnFailure = true);

        /// <summary>
        /// Experimental
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        Result GetEx(string endpoint);

        string Post(string endpoint, string body = "");
        string Post(string endpoint, JToken body);
        T Post<T>(string endpoint, string body = "");
        T Post<T>(string endpoint, JToken body);
        Task<string> PostAsync(string endpoint, string body = "");
        Task<string> PostAsync(string endpoint, JToken body);
        Task<T> PostAsync<T>(string endpoint, string body = "");
        Task<T> PostAsync<T>(string endpoint, JToken body);
        Task<HttpResponseMessage> PostHttpAsync(string endpoint, string body = "", bool throwOnFailure = true);

        string Post(string endpoint, byte[] body);
        T Post<T>(string endpoint, byte[] body);
        Task<string> PostAsync(string endpoint, byte[] body);
        Task<T> PostAsync<T>(string endpoint, byte[] body);

        string Put(string endpoint, string body);
        string Put(string endpoint, JToken body);
        T Put<T>(string endpoint, string body);
        T Put<T>(string endpoint, JToken body);
        Task<string> PutAsync(string endpoint, string body);
        Task<string> PutAsync(string endpoint, JToken body);
        Task<T> PutAsync<T>(string endpoint, string body);
        Task<T> PutAsync<T>(string endpoint, JToken body);

        string Delete(string endpoint);
        Task<string> DeleteAsync(string endpoint);
    }
}
