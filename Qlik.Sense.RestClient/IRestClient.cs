using System;
using System.Net;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Qlik.Sense.RestClient
{
    public interface IRestClient : IConnectionConfigurator
    {
        string Url { get; }
        IRestClient WithWebTransform(Action<HttpWebRequest> transform);

        string Get(string endpoint);
        Task<string> GetAsync(string endpoint);
        string Post(string endpoint, string body);
        Task<string> PostAsync(string endpoint, string body);
        byte[] Post(string endpoint, byte[] body);
        Task<byte[]> PostAsync(string endpoint, byte[] body);
        string Put(string endpoint, string body);
        Task<string> PutAsync(string endpoint, string body);
        string Delete(string endpoint);
        Task<string> DeleteAsync(string endpoint);
    }
}