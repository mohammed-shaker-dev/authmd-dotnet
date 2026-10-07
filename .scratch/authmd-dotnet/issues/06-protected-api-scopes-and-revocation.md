# 06: Protected API + Scope enforcement + revocation

**What to build:** Proof that the Access Token actually gates access, and that access can be cut off. A protected endpoint requires a valid agent Access Token with the right Scope; both a single token and a whole Agent Registration can be revoked.

**Blocked by:** 05.

**Status:** ready-for-agent

- [x] A sample protected endpoint requires a valid Access Token carrying the correct Scope
- [x] Missing token or insufficient Scope is rejected appropriately
- [x] RFC 7009 revocation invalidates an individual Access Token
- [x] Server-side Agent Registration revocation invalidates all derived tokens (surfaced as `invalid_grant` on the next re-exchange)
- [x] Verified over HTTP: authorized call succeeds; revoked token and revoked registration are both rejected

## Comments

- 2026-10-06: Implemented test-first through the HTTP seam (`tests/AuthMd.Tests/ProtectedApiTests.cs`, `RevocationTests.cs`). The protected endpoints live in the test host (`GET /api/notes` needs `api.read`, `POST /api/notes` needs `api.write`); the sample app gets its own in ticket 09.
  - New public API: `.RequireAgentScope("…")` on an endpoint or on an `AuthorizationPolicyBuilder`, `AuthMdAuthorizationExtensions.AuthenticationScheme` for `[Authorize]`, and `AgentRegistrationManager.RevokeAsync(registrationId)`.
  - `AddAuthMd<TUser>` now also registers OpenIddict validation against the local server, with token-entry validation, and calls `AddAuthorization()`.
  - No token → 401 with `WWW-Authenticate: Bearer resource_metadata="…/.well-known/oauth-protected-resource"`, which is the first hop of the discovery walk. Missing Scope → 403 with `error="insufficient_scope"` (OpenIddict's default wording is `insufficient_access`; remapped to RFC 6750's).
  - Token revocation is OpenIddict's RFC 7009 endpoint at `/oauth2/revoke`, open to anonymous clients like the token endpoint: whoever holds a token can revoke it. It returns 200 and is idempotent.
  - Registration revocation marks the row `Revoked`. It does not hunt down issued tokens; instead every API request looks the registration up by the token's `registration_id` claim and refuses the token if the registration is not active, and the `jwt-bearer` exchange does the same check and answers `invalid_grant`. Cost: one extra store read per protected request, on top of OpenIddict's token-entry read.
  - A cookie-signed-in User without a bearer token does not satisfy `RequireAgentScope`: the policy names the token scheme only. Not covered by a test.
  - There is no listing operation yet (e.g. "registrations for this User"), so a UI cannot show a User their Agents; the manager only revokes by id. Ticket 09's sample will need one if it wants a revoke page.
  - The provider-driven events endpoint (SET delivery) stays out of scope, per the spec.
