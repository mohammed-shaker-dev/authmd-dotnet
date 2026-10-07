# 04: Token endpoint — claim-grant polling (OpenIddict)

**What to build:** The Agent's polling path to its first credential. OpenIddict is wired into the token endpoint; the custom claim grant reports progress while the User hasn't finished, then yields the Agent's token and Identity Assertion once the ceremony is claimed.

**Blocked by:** 03.

**Status:** ready-for-agent

- [x] OpenIddict is wired into the Service's token endpoint
- [x] The custom claim grant returns `authorization_pending` while the Claim Ceremony is unclaimed
- [x] After confirmation it returns a standard token response plus an Identity Assertion
- [x] Polling semantics (`slow_down`, expiry) behave per spec
- [x] Verified over HTTP: poll → `authorization_pending`, confirm → token + assertion

## Comments

- 2026-10-06: Implemented test-first through the HTTP seam (`tests/AuthMd.Tests/ClaimGrantPollingTests.cs`), OpenIddict 7.7.1.
  - `AddAuthMd<TUser>` registers the OpenIddict server (token endpoint `/oauth2/token`, the `urn:workos:agent-auth:grant-type:claim` custom flow, anonymous clients, unencrypted JWT access tokens). `AddEntityFrameworkStores<TContext>` registers OpenIddict's EF Core stores and `modelBuilder.AddAuthMd<TUser>()` adds its entities, so issued tokens are stored (needed for revocation in ticket 06).
  - **Keys are the application's job.** The package registers no signing or encryption credentials; without them OpenIddict fails at startup. The test host adds ephemeral keys and disables the HTTPS requirement through its own `AddOpenIddict().AddServer(...)`. The sample (ticket 09) and README (ticket 10) need to show this; a convenience method could be added if the first-run cost is too high.
  - **OpenIddict now serves `/.well-known/oauth-authorization-server`.** It claims that path itself, so the package's own endpoint from ticket 02 was removed and the auth.md fields (`agent_auth`, restated PRM) are attached through an OpenIddict event handler. Consequences: the document gains `jwks_uri` and friends; the issuer is `http://host/` with a trailing slash (the same value as the token `iss`); `jwt-bearer` and `revocation_endpoint` are added by hand until tickets 05 and 06 enable them for real. OpenIddict also serves `/.well-known/openid-configuration` and a JWKS endpoint by default.
  - The Identity Assertion is a JWT signed by the package with OpenIddict's signing credential: `typ` `agent-identity-assertion+jwt`, `iss`/`aud` the issuer, `sub` the User id, plus `registration_id`, `scope`, `jti`. Lifetime `IdentityAssertionLifetime`, default 7 days (upstream gives no number). The access token is OpenIddict's, default 1 hour, with `sub` and `registration_id`.
  - Token-endpoint errors are HTTP 400 per RFC 6749/8628. The upstream implementer guide shows `authorization_pending` with a 200; this follows the RFCs instead.
  - `slow_down`: a poll within `PollingInterval` of the previous one. Every poll resets the timer, including a too-fast one.
  - A `claim_token` yields tokens once; a later poll gets `invalid_grant`. A claimed ceremony stays redeemable past its expiry, so a User who confirms at the last second does not strand the Agent.
  - The token is minted from the claimed ceremony (User id, Scopes, registration id). The Agent Registration row is still not read back; ticket 05's `jwt-bearer` exchange is where it has to be, and ticket 03's open box closes there.
  - `unsupported_grant_type` is OpenIddict's own behaviour; its test passed on first run.
