# 08: AuthMd.Client — service_auth flow + end-to-end ceremony test

**What to build:** The Agent SDK driving a registration from start to protected call, and the headline proof that the two packages work together. The client submits the User's email, surfaces the `user_code`, polls, obtains and uses an Access Token, and re-exchanges on expiry. One end-to-end test runs the whole ceremony through the single seam.

**Blocked by:** 06, 07.

**Status:** ready-for-agent

- [x] The client runs the `service_auth` flow: submits the email, surfaces the `user_code` to the caller, polls the token endpoint with correct backoff
- [x] It exchanges to an Access Token, attaches `Authorization: Bearer`, and re-exchanges the Identity Assertion on expiry
- [x] Typed errors are raised for `invalid_grant` / expired assertion so the caller knows to restart registration
- [x] Headline end-to-end test: the client drives the full ceremony against the Service through the same in-memory `WebApplicationFactory` seam, ending in a successful protected API call

## Comments

- 2026-10-07: Implemented test-first through the HTTP seam (`tests/AuthMd.Tests/ClientCeremonyTests.cs`). The headline test is `Agent_registers_on_behalf_of_a_user_and_calls_the_protected_api`.
  - Shape of the SDK:
    - `AuthMdClient.RegisterAsync(service, email)` → `PendingRegistration` (`UserCode`, `VerificationUri`, `ExpiresAt`, `Interval`, `Scopes`). The `claim_token` stays private to it.
    - `PendingRegistration.WaitForUserAsync()` → `AgentSession`. Waits `Interval` before each poll, adds 5 s on `slow_down`, throws `RegistrationRequiredException` on `expired_token`/`invalid_grant`.
    - `AgentSession`: `IdentityAssertion`, `RegistrationId`, `GetAccessTokenAsync()`, `RevokeAccessTokenAsync()`, and `CreateHandler()`, a `DelegatingHandler` that attaches the bearer token.
  - Token renewal: a token within 30 s of expiry is replaced before use; a 401 from the API triggers one re-exchange and one retry. The retry only happens for requests without a body (a body may not be readable twice); a POST that gets a 401 is returned as-is, though the next call will carry a new token.
  - Errors: `AuthMdProtocolException` (carries `Error`, `ErrorDescription`) for anything the Service refuses; `RegistrationRequiredException` derives from it for "start over with the User".
  - Every exchange sends `resource` = the discovered resource URL.
  - Tests share one fake clock between Service and Agent and advance it to release the Agent's polling delays. Two tests also sleep briefly in real time (50–200 ms) to let an in-flight poll land before the "human" acts; they have been stable but are the place to look if a test ever flakes.
  - `slow_down` is provoked by a test handler that delivers the Agent's first poll twice, as a flaky network would.
  - The four session tests (expiry, revoked token, expired assertion, revoked registration) were written together and then implemented together, rather than one at a time. The pacing test passed on first run because the delay was already part of the headline implementation.
  - Not done: asking for a subset of Scopes from the SDK (the server supports `scope` on the exchange; the client always takes the full set); restoring an `AgentSession` from a saved Identity Assertion in a new process; thread-safety is by a single lock per session and is not tested under concurrency.
