# qlik_rest_sdk
This is a basic SDK for Qlik Sense REST access with authentication configuration built along the same line as the .Net SDK for the Engine API. Basic usage for accessing the "/qrs/about" endpoint could look like this:

```
var restClient = new RestClient(senseServerUrl);
restClient.AsNtlmUserViaProxy();
Console.WriteLine(restClient.Get("/qrs/about"));
```

Currently only NTLM authentication and authentication using exported certificates are supported.

The library is also available from NuGet: https://www.nuget.org/packages/QlikSenseRestClient/
