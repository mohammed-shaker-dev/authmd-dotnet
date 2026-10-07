# 02: Server surface + discovery documents + service_auth registration

**What to build:** Stand the Service up. A developer adds the package to an ASP.NET Core Identity app via `AddAuthMd(...)` + `MapAuthMd()`, and the app immediately serves the auth.md discovery documents and accepts a `service_auth` registration that issues a Claim Ceremony. This is the "server comes alive" slice (discovery folded in here).

**Blocked by:** 01.

**Status:** ready-for-agent

- [x] `AddAuthMd(...)` and `MapAuthMd()` wire the Service into an ASP.NET Core Identity host
- [x] `/auth.md`, the RFC 9728 Protected Resource Metadata (`/.well-known/oauth-protected-resource`), and the authorization-server metadata (including the agent block and supported Scopes) are served from configuration
- [x] `POST /agent/identity` with a `service_auth` body returns `claim_token`, `user_code`, and `verification_uri`
- [x] Store abstractions exist with an EF Core default implementation; a `pending` Claim Ceremony is persisted
- [x] An ASP.NET Core Identity test host exists for the `WebApplicationFactory` seam
- [x] Verified over HTTP: documents fetched and asserted; registration returns a well-formed claim

## Comments

- 2026-10-06: Implemented test-first through the HTTP seam (`tests/AuthMd.Tests`, host in `tests/AuthMd.TestHost` on Identity + in-memory SQLite). Decisions made along the way, all revisitable:
  - Protocol shapes follow upstream `AUTH.md` (github.com/workos/auth.md). `agent_auth.identity_types_supported` is `["service_auth"]`; `events_endpoint` and the `identity_assertion` block are omitted (out of scope for v1).
  - The authorization-server metadata is served by the package itself for now. OpenIddict is not referenced yet; ticket 04 wires it into the token endpoint and can take the document over without changing the tests.
  - One `ClaimLifetime` (default 10 min) drives both `claim.expires_in` and `claim_token_expires`: v1 has no `/agent/identity/claim` re-mint, so a separate 24h outer window would promise something the Service cannot do. `/auth.md` tells the Agent to restart at registration on `expired_token`.
  - `verification_uri` is `{base}{ClaimPagePath}?claim_attempt_token=…` (default path `/claim`), the consumer's own page (ADR 0003). Its `[Authorize]` supplies the Identity login redirect.
  - `claim_token` and `claim_attempt_token` are stored as SHA-256 hashes; `user_code` is stored as issued.
  - Registration does not look the User up: the email in `login_hint` is matched at confirmation (ticket 03), so the endpoint does not reveal which emails have accounts.
  - Refusals: `anonymous` → `anonymous_not_enabled`, `identity_assertion` → `issuer_not_enabled`, anything malformed → `invalid_request`, all HTTP 400 with `{ error, error_description }`.
  - EF Core wiring is `AddAuthMd(...).AddEntityFrameworkStores<TContext>()` plus `modelBuilder.AddAuthMd()` in the consumer's `OnModelCreating`. No migrations are shipped: the tables live in the consumer's context, so the consumer generates them. This differs from the spec's "EF migrations shipped".
  - Persistence of the pending ceremony is exercised (the registration test writes through EF to SQLite) but not asserted directly: nothing reads a ceremony back over HTTP until ticket 03.
  - Only `IClaimCeremonyStore.CreateAsync` exists so far; `IAgentRegistrationStore` arrives with ticket 03, which is the first ticket that needs it.
