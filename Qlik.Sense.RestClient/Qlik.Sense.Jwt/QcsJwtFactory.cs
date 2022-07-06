using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Qlik.Sense.Jwt
{
    /// <summary>
    /// Class for producing JSON Web Tokens for accessing Qlik Cloud Services.
    /// </summary>
    public class QcsJwtFactory
    {
        private readonly SigningCredentials _signingCredentials;
        private readonly string _issuer;
        public TimeSpan ExpirationTime { get; set; }

        private static readonly string[] SupportedSecurityAlgorithms = 
        {
            SecurityAlgorithms.RsaSha384,
            SecurityAlgorithms.RsaSha512
        };

        /// <summary>
        /// Create a JWT factory instance that can produce JSON Web Tokens for accessing Qlik Cloud Services.
        /// </summary>
        /// <param name="path">The path to the private key file.</param>
        /// <param name="keyId">Value to use for the key ID field.</param>
        /// <param name="issuer">Value to use for the issuer field.</param>
        /// <param name="securityAlgorithm">The encryption algorithm to use when producing tokens. Supported values are <c>RS384</c> and <c>RS512</c>.</param>
        public QcsJwtFactory(string path, string keyId, string issuer, string securityAlgorithm = SecurityAlgorithms.RsaSha512)
        {
            if (!SupportedSecurityAlgorithms.Contains(securityAlgorithm))
            {
                throw new ArgumentException($"Unsupported security algorithm: {securityAlgorithm}", nameof(securityAlgorithm));
            }

            var rsa = KeyFromPemFile(path);
            _signingCredentials = new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = keyId }, securityAlgorithm);
            _issuer = issuer;
            ExpirationTime = TimeSpan.FromSeconds(30);
        }

        private static RSA KeyFromPemFile(string path)
        {
            var pemFileContents = File.ReadAllLines(path);
            var pemFileKey = string.Join("", pemFileContents.Skip(1).SkipLast(1));
            var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(Convert.FromBase64String(pemFileKey), out _);
            return rsa;
        }

        /// <summary>
        /// Produce a JWT that can be used to connect to QCS as a specific user.
        /// </summary>
        /// <param name="subject">The subject identifier of the user.</param>
        /// <param name="name">The name of the user.</param>
        /// <returns>A JWT to use when connecting to QCS.</returns>
        public string MakeJwt(string subject, string name)
        {
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                // Required claims according to https://qlik.dev/tutorials/create-signed-tokens-for-jwt-authorization
                // {
                //     "jti": "k5bU_cFI4_-vFfpJ3DjDsIZK-ZhJGRbBfusUWZ0ifBI",
                //     "sub": "SomeSampleSeedValue", //e.g. 0hEhiPyhMBdtOCv2UZKoLo4G24p-7R6eeGdZUQHF0-c
                //     "subType": "user",
                //     "name": "Hardcore Harry",
                //     "email": "harry@example.com",
                //     "email_verified": true,
                //     "groups": ["Administrators", "Sales", "Marketing"]
                // }
                Claims = new Dictionary<string, object>()
                {
                    {"jti", Guid.NewGuid().ToString()},
                    {"sub", subject},
                    {"name", name},
                },
                Expires = DateTime.UtcNow.Add(ExpirationTime),
                Issuer = _issuer,
                Audience = "qlik.api/login/jwt-session",

                SigningCredentials = _signingCredentials
            };
            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}