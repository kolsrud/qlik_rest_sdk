﻿using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace Qlik.Sense.RestClient
{
    public interface IConnectionConfigurator
    {
        void AsDirectConnection(int port = 4242, bool certificateValidation = true, X509Certificate2Collection certificateCollection = null);

        void AsDirectConnection(string userDirectory, string userId, int port = 4242, bool certificateValidation = true, X509Certificate2Collection certificateCollection = null);

        void AsNtlmUserViaProxy(bool certificateValidation = true);

        void AsNtlmUserViaProxy(NetworkCredential credential, bool certificateValidation = true);

        void AsStaticHeaderUserViaProxy(string userId, string headerName, bool certificateValidation = true);

        void AsExistingSessionViaProxy(string sessionId, string cookieHeaderName, bool proxyUsesSsl = true, bool certificateValidation = true);
    }
}