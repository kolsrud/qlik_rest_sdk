using System;
using System.Security;
using Newtonsoft.Json.Linq;
using Qlik.Sense.RestClient;

namespace BasicConnectionCertificateFromFile
{
    class Program
    {
        static void Main(string[] args)
        {
            var url = "<url>";
            var restClient = new RestClient(url);

            var securePassword = new SecureString();
            foreach (var c in "mypassword".ToCharArray())
            {
                securePassword.AppendChar(c);
            }

            var certs = RestClient.LoadCertificateFromDirectory("path/to/certs", securePassword);
            restClient.AsDirectConnection(4242, false, certs);
            Console.WriteLine(restClient.Get<JToken>("/qrs/about"));
        }
    }
}
