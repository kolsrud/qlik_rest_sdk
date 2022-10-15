# Release Notes for Qlik Sense Rest Client

## v1.12.0
* **NEW FEATURE:** Added property `RestClient.CustomUserAgent` to configure HTTP user-agent header to use for application.

## v1.11.0
* **NEW FEATURE:** Property `RestClient.QcsSessionClient` is now exposed through interface `IRestClient`.
* **NEW FEATURE:** Method `RestClient.PostHttpAsync` overload with `JToken` body is now exposed through interface `IRestClient`.
* **BUG FIX:** Authentication is now performed automatically also when calling `PostHttpAsync` as first endpoint.
* **BUG FIX:** The csrf-token should not be provided as argument for REST calls to QCS.

## v1.10.0
* **NEW FEATURE:** Added connection type `AsExistingSessionViaQcs`.
* **BUG FIX:** Fixed issue with set of claims used for generating JWTs.

## v1.9.0
* **NEW FEATURE:** Added connection type `AsJsonWebTokenViaQcs`.
* **NEW FEATURE:** Added dll `Qlik.Sense.Jwt` containing the class `QcsJwtFactory` for producing JSON Web Tokens. Only available for .NET Core 3.1.
* **DEPRECATED:** Deprecated support for .NET Framework 4.5.2 and .NET Core 2.1. Minimum supported versions are .NET Framework 4.6.2 and .NET Core 3.1.

## v1.8.0
* **NEW FEATURE:** It is now possible to configure the proxy to use for REST connections through the property `RestClient.Proxy`.
* **NEW FEATURE:** Added connection type `AsExistingSessionViaProxy`.
* **DEPRECATED:** Deprecated class `DebugConsole`. Use new class `RestClientDebugConsole` instead. Name switch performed to avoid name clash with Qlik Sense .NET SDK.

## v1.7.0
* **NEW FEATURE:** Added methods IRestClient.GetHttpAsync and IRestClient.PostHttpAsync.

## v1.6.0
* **NEW FEATURE:** Added endpoints `IRestClient.GetStream` and `IRestClient.GetStreamAsync`.

## v1.5.0
* **NEW FEATURE:** Added access type `AsAnonymousUserViaProxy`.
* **NEW FEATURE:** Added constructor `RestClient(Uri uri)`.

## v1.4.0
* **NEW FEATURE:** Added overload RestClient.LoadCertificateFromDirectory(string path, string certificatePassword)

## v1.3.1
* **NEW FEATURE:** Added experimental feature for collecting request statistics.

## v1.3.0
* **NEW FEATURE:** Added `Post` and `Put` endpoints that accepts JToken objects as body.
* **NEW FEATURE:** Added `Get`, `Post` and `Put` endpoints that return generic types based on JSON deserialization.
* **NEW FEATURE:** Added endpoint `IRestClient.User`.
* **NEW FEATURE:** Added endpoints `IRestClient.GetBytes` and `IRestClient.GetBytesAsync` for downloading binary data.

## v1.2.0
* **NEW FEATURE:** Added endpoint `ClientFactory.ClearRuleCache`.
* **BUG FIX:** Static header connection did not respect the certificatValidation argument.

## v1.1.0
* **NEW FEATURE:** Added User constructor User(string usr).
* **DEPRECATED:** Deprecated methods `AsJwtTokenViaProxy` and `AsJwtTokenViaQcs`. Renamed to `AsJwtViaProxy` and `AsJwtViaQcs`.

## v1.0.0
* **NEW FEATURE:** Support for JWT Authentication towards both QSEfW and QCS.
* **NEW FEATURE (.NET Framework only):** Support for TLS 1.1 and TLS 1.2 is now on by default.

## v0.11.0
* **NEW FEATURE:** Added the `ClientFactory` concept for simplifying impersonation and simulation.

## v0.10.0
* **NEW FEATURE:** Rest client configuration methods are now exposed in the `IRestClient` class.
* **NEW FEATURE:** Added rest client configuration methods for choosing to connect as QMC or HUB. Default is Both.
* **NEW FEATURE:** Added possibility of configuring Xrfkey.
* **NEW FEATURE:** Exposed configured UserId and UserDirectory in IRestClient.
* **NEW FEATURE:** Added support for `KeyStorageFlag` setting when loading certificates from file.
* **BUG FIX:** Added use of the infamous ConfigureAwait for async calls to prevent possible deadlocks in GUI and other "special thread" contexts.
