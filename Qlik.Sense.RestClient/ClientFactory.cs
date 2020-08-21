using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json;
using Qlik.Sense.RestClient.Qrs;

namespace Qlik.Sense.RestClient
{
    public class ClientFactory
    {
        private readonly string _url;
        private readonly X509Certificate2Collection _certs;
        private readonly bool _connectAsQmc;

        public ClientFactory(string url, X509Certificate2Collection certs, bool connectAsQmc = true)
        {
            _url = url;
            _certs = certs;
            _connectAsQmc = connectAsQmc;
        }

        private IRestClient _adminClient = null;

        public IRestClient AdminClient => _adminClient ?? (_adminClient = GetClient("INTERNAL", "sa_api"));

        public IRestClient GetClient(User user) 
        {
            return GetClient(user.Directory, user.Id);
        }

        public IRestClient GetClient(string userDirectory, string userId)
        {
            var client = new RestClient(_url);
            client.AsDirectConnection(userDirectory, userId, 4242, false, _certs);
            return _connectAsQmc ? client.ConnectAsQmc() : client.ConnectAsHub();
        }

        public IEnumerable<User> GetAllUsers()
        {
            return JsonConvert.DeserializeObject<List<User>>(AdminClient.Get("/qrs/user")).Where(u => u.Directory != "INTERNAL");
        }

        public void ClearRuleCache()
        {
            AdminClient.Post("/qrs/systemrule/security/resetcache");
        }
    }
}