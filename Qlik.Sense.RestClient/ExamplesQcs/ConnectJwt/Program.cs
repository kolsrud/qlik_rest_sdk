using System;
using Newtonsoft.Json.Linq;
using Qlik.Sense.Jwt;
using Qlik.Sense.RestClient;

namespace ConnectJwt
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var url = "<url>";
            var privateKeyPath = @"C:\path\to\privatekey.pem";
            var keyId = "<keyId>";
            var issuer = "<issuer>";
            var subject = "<subjectID>";
            var name = "<userName>";

            var jwtFactory = new QcsJwtFactory(privateKeyPath, keyId, issuer);
            var jwt = jwtFactory.MakeJwt(subject, name);

            var restClient = new RestClient(url);
            restClient.AsJsonWebTokenViaQcs(jwt);

            Console.WriteLine(restClient.Get<JToken>("/api/v1/users/me"));
        }
    }
}
