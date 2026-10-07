# 05: Token endpoint — jwt-bearer re-exchange

**What to build:** Keeping an Agent working past token expiry without a refresh token. The Agent re-exchanges its stored Identity Assertion for a fresh Access Token via the RFC 7523 `jwt-bearer` grant.

**Blocked by:** 04.

**Status:** ready-for-agent

- [x] The RFC 7523 `jwt-bearer` grant exchanges a valid Identity Assertion for a new Access Token
- [x] No refresh token is issued; expiry is handled purely by re-exchange
- [x] An expired or revoked assertion returns `invalid_grant`
- [x] Optional resource pinning on the exchange is honoured
- [x] Verified over HTTP: exchange succeeds, and a stale assertion is rejected

## Comments

- 2026-10-06 (later): Third box closed by ticket 06, whose registration-revocation test asserts the exchange answers `invalid_grant`.

- 2026-10-06: Implemented test-first through the HTTP seam (`tests/AuthMd.Tests/JwtBearerExchangeTests.cs`). The third box is half done: an expired assertion is rejected; a revoked one cannot be tested until ticket 06 adds a way to revoke an Agent Registration, and the status check lands with that test.
  - The exchange validates the assertion (signature against OpenIddict's signing keys, issuer, audience, token type, expiry against the Service's clock with no skew), then loads the Agent Registration and mints the token from the row: User id and Scopes come from the registration, not from the assertion's claims.
  - Token type matters: Access Tokens are signed with the same key, and once they carry an audience equal to the issuer only `typ` (`agent-identity-assertion+jwt`) separates the two. A test covers presenting an Access Token as an assertion.
  - Resource: every Access Token (both grants) now carries `aud` = the Service's resource URL. A `resource` parameter is accepted when it equals that URL and rejected with `invalid_target` (RFC 8707) otherwise. OpenIddict's own static resource check is disabled because the URL can be request-derived.
  - Scope: an optional `scope` parameter narrows the token to a subset of the registration's Scopes (user story 14). Unknown scopes are rejected by OpenIddict as `invalid_scope`; a known scope the registration lacks is rejected the same way by the package, but no test can reach that until registrations can carry fewer than all Scopes.
  - The assertion is reusable until it expires; the exchange response carries no `identity_assertion` and no `refresh_token`.
  - The forged/tampered-assertion test passed on first run (signature validation is the JWT handler's default).
