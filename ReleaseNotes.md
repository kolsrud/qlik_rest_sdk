# Release Notes for Qlik Sense Rest Client

## v1.1.1

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
