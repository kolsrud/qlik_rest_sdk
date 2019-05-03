using System.Threading.Tasks;

namespace Qlik.Sense.RestClient
{
    public interface IRestClient : IConnectionConfigurator
    {
        string Url { get; }

        bool Authenticate();

        string Get(string endpoint);
        Task<string> GetAsync(string endpoint);
        string Post(string endpoint, string body);
        Task<string> PostAsync(string endpoint, string body);
        string Post(string endpoint, byte[] body);
        Task<string> PostAsync(string endpoint, byte[] body);
        string Put(string endpoint, string body);
        Task<string> PutAsync(string endpoint, string body);
        string Delete(string endpoint);
        Task<string> DeleteAsync(string endpoint);
    }
}