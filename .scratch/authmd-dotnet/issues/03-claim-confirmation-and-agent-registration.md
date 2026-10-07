# 03: Claim confirmation (human step) → claimed + Agent Registration

**What to build:** The human-approval half of the Claim Ceremony. A User authenticates with their existing ASP.NET Core Identity account, confirms the `user_code`, and the ceremony becomes `claimed` — creating an Agent Registration bound to that User with the granted Scopes.

**Blocked by:** 02.

**Status:** ready-for-agent

- [x] An authenticated confirm operation (as a seeded Identity User) moves the Claim Ceremony `pending → claimed`
- [x] A persistent Agent Registration is created, foreign-keyed to the `AspNetUsers` row, carrying the granted Scopes
- [x] An expired or invalid `user_code` is rejected with a spec-compliant error
- [x] Verified over HTTP through the seam: confirm, then observe claimed state + linked registration

## Comments

- 2026-10-06 (later): Last box closed by ticket 05. The `jwt-bearer` exchange mints its token from the Agent Registration row, and its test asserts the token's `sub` is the confirming User's id.

- 2026-10-06: Implemented test-first through the HTTP seam (`tests/AuthMd.Tests/ClaimConfirmationTests.cs`). The last box stays open on its second half: the claimed state is observed over HTTP, but nothing in this ticket's surface reads an Agent Registration back. The insert runs against SQLite with the foreign key enforced, so a broken link would fail the confirm; the registration becomes directly observable when ticket 04 mints a token from it.
  - Page contract operations, both under the advertised claim endpoint and both requiring the signed-in Identity User:
    - `GET /agent/identity/claim/view?claim_attempt_token=…` → `{ registration_id, status, login_hint, scopes, expires_at }`, `status` is `pending`, `claimed` or `expired`.
    - `POST /agent/identity/claim/confirm` with JSON `{ claim_attempt_token, user_code }` → the same shape with `status: "claimed"`.
  - Errors: nobody signed in → 401 `login_required`; unknown token → 400 `invalid_claim_token`; signed-in User's email differs from `login_hint` → 403 `access_denied`; already claimed → 409 `claimed_or_in_flight`; expired → 410 `claim_expired`; wrong `user_code` → 400 `invalid_request`. Upstream defines no code for a wrong `user_code` (it never reaches the Agent), so `invalid_request` is this package's choice; `login_required` and `access_denied` are borrowed from OIDC/OAuth for the same reason.
  - `AddAuthMd` and `modelBuilder.AddAuthMd` are now generic over the Identity user type (`AddAuthMd<TUser>`). The User is resolved through `UserManager<TUser>`, and emails are compared with Identity's normalizer.
  - Limitation: the foreign keys assume a string user key (`IdentityUser<string>`). Apps with `Guid`/`int` keys are not supported yet.
  - The Agent Registration reuses the `registration_id` issued at registration as its key, so a double confirm cannot create two registrations.
  - No attempt limit on `user_code`: guessing requires the 256-bit `claim_attempt_token` and being signed in as the named User.
  - The confirm endpoint only accepts `application/json`, which together with Identity's SameSite=Lax cookie is the CSRF defence. Worth a second look when the sample claim page is built (ticket 09).
  - The registration request carries no Agent name, so the view has nothing to show for "which Agent"; the page can show the Scopes and the email.
