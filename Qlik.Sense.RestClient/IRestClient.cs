using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Qlik.Sense.RestClient
{
    public interface IRestClient
    {
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

        string Post(string endpoint, string body = "");
        string Post(string endpoint, JToken body);
        T Post<T>(string endpoint, string body = "");
        T Post<T>(string endpoint, JToken body);
        Task<string> PostAsync(string endpoint, string body = "");
        Task<string> PostAsync(string endpoint, JToken body);
        Task<T> PostAsync<T>(string endpoint, string body = "");
        Task<T> PostAsync<T>(string endpoint, JToken body);

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