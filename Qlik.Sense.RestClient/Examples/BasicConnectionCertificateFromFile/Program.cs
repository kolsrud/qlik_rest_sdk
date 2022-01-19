using System;
using System.Linq;
using System.Security;
using Qlik.Sense.RestClient;

namespace BasicConnectionCertificateFromFile
{
    class Program
    {
        static void Main(string[] args)
        {
            var senseServerUrl = args.Any() ? args[0] : "https://my.server.url";
            var restClient = new RestClient(senseServerUrl);

            var securePassword = new SecureString();
            foreach (var c in "mypassword".ToCharArray())
            {
                securePassword.AppendChar(c);
            }

            var certs = RestClient.LoadCertificateFromDirectory("path/to/certs", securePassword);
            restClient.AsDirectConnection(4242, false, certs);
            using (new RestClientDebugConsole())
                restClient.Get("/qrs/about");
        }
    }
}
