# Release Notes for Qlik Sense Rest Client

## v1.8.0

* **NEW FEATURE:** It is now possible to configure the proxy to use for REST connections through the property `RestClient.Proxy`.

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
* **BUGFIX:** Static header connection did not respect the certificatValidation argument.

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
