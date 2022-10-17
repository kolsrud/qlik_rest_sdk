# Qlik Sense REST SDK #
This is a basic SDK for Qlik Sense REST access with authentication configuration built along the same line as the .Net SDK for the Engine API. Basic usage for accessing the "/qrs/about" endpoint could look like this:

```
var restClient = new RestClient(senseServerUrl);
restClient.AsNtlmUserViaProxy();
Console.WriteLine(restClient.Get("/qrs/about"));
```

#### Supported .NET versions ####
Supported .NET versions are .NET Framework 4.6.2 and .NET Core 3.1 and .NET releases compatible with those two versions that are officially supported by Microsoft. 

#### Supported authentication mechanisms ####
This library currently supports the following authentication mechanisms:

* Qlik Sense Enterprise for Windows (QSEfW)
  * Direct connection using certificates
  * NTLM authentication using default or custom credentials.
  * Static header authentication.
  * JWT authentication
  * Connection to existing session.
* Qlik Cloud Services (QCS)
  * API Key authentication
  * JWT authentication
  * Client credential authentication
  * Connection to existing session.

The library is available for download from NuGet: https://www.nuget.org/packages/QlikSenseRestClient/
