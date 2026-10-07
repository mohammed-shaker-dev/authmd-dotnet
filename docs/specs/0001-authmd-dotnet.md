# Spec 0001 — authmd-dotnet (v1)

> Status: ready-for-agent (pending issue tracker — publish via `/setup-matt-pocock-skills`)
> Date: 2026-10-06
> Vocabulary: see [CONTEXT.md](../../CONTEXT.md). Respects ADRs
> [0001](../adr/0001-openiddict-as-oauth-engine.md),
> [0002](../adr/0002-service-auth-first.md),
> [0003](../adr/0003-headless-server-no-ui-package.md).

## Problem Statement

AI agents increasingly need to act on behalf of people against real web
applications, but there is no clean way for an agent to register and
authenticate with an ASP.NET Core app without a human manually driving a
browser sign-up form and OAuth consent screen. WorkOS's **auth.md** protocol
solves this in the abstract, but there is no .NET implementation — so a
developer running an ASP.NET Core Identity app has no drop-in way to become
agent-ready, and an agent author has no .NET SDK to talk to such a service.

From the developer's perspective: "I have an ASP.NET Core Identity site. I want
AI agents to be able to register and sign in on behalf of my users, securely
and on standards, without me hand-building an agent auth protocol."

## Solution

Two NuGet packages in the `authmd-dotnet` monorepo (net10.0):

- **`AuthMd.AspNetCore`** — a server library that makes an existing ASP.NET
  Core Identity app auth.md-compliant. Built on OpenIddict (ADR 0001). The
  developer calls `AddAuthMd(...)` + `MapAuthMd()`; the package serves
  `/auth.md`, the RFC 9728 Protected Resource Metadata and authorization-server
  metadata, and the agent registration / Claim Ceremony / token endpoints,
  minting credentials tied to ASP.NET Core Identity Users.
- **`AuthMd.Client`** — an agent-side SDK that discovers a service's `auth.md`,
  runs the `service_auth` Claim Ceremony, exchanges for an Access Token, and
  uses/refreshes it to call protected APIs.

v1 implements the **`service_auth`** flow only (ADR 0002): the Agent presents
the User's email, the Service returns a `user_code` + verification URL, the
**human signs in through the app's existing Identity login** and confirms, and
the Agent polls the token endpoint to receive credentials. The server is
**headless** (ADR 0003): the claim/consent page is the consumer's, built to a
documented contract; the sample app ships a real one.

## User Stories

### The Agent (AI acting on behalf of a human)

1. As an Agent, I want to fetch `/auth.md` from a Service, so that I can learn
   how to register without a human reading docs for me.
2. As an Agent, I want to discover the Protected Resource Metadata via the
   two-hop walk (`WWW-Authenticate` header or the well-known path), so that I
   can find the authorization server and supported scopes.
3. As an Agent, I want to read the authorization-server metadata, so that I can
   locate the token, revocation, and agent endpoints and the supported grant
   types.
4. As an Agent, I want to determine that `service_auth` is the applicable
   registration method, so that I pick the correct flow for the state I'm in
   (I have the User's email but no prior session).
5. As an Agent, I want to submit a `service_auth` registration with the User's
   email as `login_hint`, so that I can begin registering on their behalf.
6. As an Agent, I want to receive a `claim_token` plus a `user_code` and
   `verification_uri`, so that I can hand the human what they need to approve
   me.
7. As an Agent, I want to present the `user_code` and verification URL to the
   human in my own UI, so that the human can go approve the registration.
8. As an Agent, I want to poll the token endpoint with the claim grant, so that
   I obtain credentials as soon as the human finishes.
9. As an Agent, I want `authorization_pending` and `slow_down` handled for me
   by the SDK, so that I poll correctly without hammering the server.
10. As an Agent, I want to receive an Identity Assertion and exchange it for an
    Access Token, so that I can call the Service's protected APIs.
11. As an Agent, I want the SDK to attach `Authorization: Bearer <token>`
    automatically, so that calling a protected endpoint is a one-liner.
12. As an Agent, I want the SDK to transparently re-exchange the Identity
    Assertion when the Access Token expires, so that long-running work doesn't
    break on expiry.
13. As an Agent, I want clear typed errors when the assertion itself is expired
    or revoked (`invalid_grant`), so that I know to restart registration.
14. As an Agent, I want to request and receive only the Scopes I need, so that
    I hold least privilege.
15. As an Agent, I want to revoke my own Access Token, so that I can clean up
    after a task.

### The User (human account owner)

16. As a User, I want to approve an Agent by entering a short code on my
    provider's own page, so that I know I'm approving on the real site and not
    a phishing proxy.
17. As a User, I want to authenticate with my existing ASP.NET Core Identity
    credentials during approval, so that I don't need a new account or flow.
18. As a User, I want to see which Agent and which Scopes I'm approving, so
    that I can make an informed decision.
19. As a User, I want the code to expire quickly if unused, so that a leaked
    code is low-risk.
20. As a User, I want an Agent's registration to be revocable, so that I can
    cut off an Agent later without changing my password.
21. As a User, I want the Service never to email me the code, so that the
    approval can't be driven purely by someone who compromised my inbox.

### The Service developer (consumer of the packages)

22. As a Service developer, I want to add agent auth with
    `builder.Services.AddAuthMd(...)` and `app.MapAuthMd()`, so that I enable
    the whole protocol with minimal wiring.
23. As a Service developer, I want it to build on my existing ASP.NET Core
    Identity users, so that agents act on the same accounts my humans use.
24. As a Service developer, I want `/auth.md` and the OAuth metadata generated
    for me from my configuration, so that I don't hand-author protocol
    documents.
25. As a Service developer, I want to declare the Scopes my API supports, so
    that they appear in metadata and constrain Access Tokens.
26. As a Service developer, I want a documented page contract for the claim
    page, so that I can build it in Razor Pages, MVC, Blazor, or a SPA.
27. As a Service developer, I want an EF Core store out of the box with
    migrations, so that persistence works with zero custom code.
28. As a Service developer, I want to swap in my own store via an interface,
    so that I can use a different persistence technology.
29. As a Service developer, I want Agent Registrations linked to my
    `AspNetUsers` rows, so that I can query and manage which agents act for
    which users.
30. As a Service developer, I want to revoke an Agent Registration server-side,
    so that I can terminate an agent's access for a user.
31. As a Service developer, I want standards-compliant error responses
    (correct OAuth/auth.md error codes), so that any conformant agent
    interoperates.
32. As a Service developer, I want to protect an API endpoint such that a valid
    agent Access Token with the right Scope is required, so that agents can
    only do what they're authorized to.
33. As a Service developer, I want the package to target net10.0, so that it
    fits a current .NET stack.

### The maintainer / portfolio author

34. As the maintainer, I want a runnable two-app demo, so that I can record the
    whole ceremony for a LinkedIn post.
35. As the maintainer, I want CI that builds, tests, and packs on every push,
    so that the repo signals engineering seriousness.
36. As the maintainer, I want one end-to-end integration test of the full
    ceremony, so that the headline claim is backed by executable proof.
37. As the maintainer, I want an MIT license, so that the package is freely
    adoptable.
38. As the maintainer, I want a README with a sequence diagram and quickstart,
    so that a reader understands auth.md and this package in under two minutes.
39. As the maintainer, I want to publish to GitHub first and NuGet.org once
    stabilized, so that I control the quality bar before a public feed listing.

## Implementation Decisions

### Modules

- **`AuthMd.AspNetCore`** (server). Public surface: `AddAuthMd(options)` DI
  extension and `MapAuthMd()` endpoint-routing extension. Internally wires
  OpenIddict (token endpoint, custom claim grant handler, revocation, server
  metadata) and adds the auth.md layer. Owns generation of the `/auth.md`
  document, the RFC 9728 PRM at `/.well-known/oauth-protected-resource`, and
  the authorization-server metadata including the agent-specific block.
- **`AuthMd.AspNetCore` stores**. `IAgentRegistrationStore` and a
  claim-ceremony store abstraction, with an EF Core default implementation
  whose entities are foreign-keyed to `AspNetUsers`; EF migrations shipped.
- **`AuthMd.Client`** (agent SDK). Discovery (auth.md + PRM + AS metadata),
  the `service_auth` Claim Ceremony (surface `user_code`, poll with backoff),
  RFC 7523 token exchange, and an `HttpClient`/handler that attaches the bearer
  token and re-exchanges the assertion on expiry.
- **`samples/`** — a sample ASP.NET Core Identity web Service (publishes
  `auth.md`, hosts a reference claim/consent page, exposes one protected API)
  and a console Agent app using `AuthMd.Client`.

### Flow & protocol (v1 = `service_auth` only)

- Registration endpoint accepts the `service_auth` body (`type`,
  `login_hint`) and returns `claim_token` + a `claim` block (`user_code`,
  `verification_uri`). `identity_assertion` and `anonymous` bodies are not
  implemented in v1 but the dispatch is structured to add them.
- Claim Ceremony state is `pending → claimed` with expiry; the human confirms
  via the Service-owned page after authenticating with Identity. The Service
  never emails the code.
- Token endpoint supports the auth.md custom claim grant (returns
  `authorization_pending` until claimed, then a standard token response plus
  the Identity Assertion) and the RFC 7523 `jwt-bearer` exchange (assertion →
  Access Token; no refresh token). Revocation via RFC 7009 for tokens, and a
  server-side operation to revoke an Agent Registration.

### Data / schema

- An Agent Registration entity: links to a User (`AspNetUsers`), carries
  granted Scopes and status, and is the unit of revocation.
- A Claim Ceremony entity: `user_code`, `claim_token`, status, expiry,
  requested Scopes, and (once claimed) the resolved User.
- OpenIddict's own stores persist tokens/authorizations; auth.md-specific
  state uses the stores above.

### Page contract (headless, ADR 0003)

- A documented contract describes what the consumer's claim page must do:
  accept/display the `user_code`, require Identity authentication, show the
  requesting Agent and Scopes, and call the confirm operation on approval.

## Testing Decisions

- **What a good test is here:** it asserts *external behavior at the HTTP
  boundary* — the documents and tokens a conformant agent would observe
  (metadata contents, issued tokens, granted scopes, spec-compliant error
  codes, revocation effects) — never internal store rows, handler calls, or
  OpenIddict wiring details.
- **Single seam (confirmed):** the HTTP boundary of `AuthMd.AspNetCore`,
  driven via `WebApplicationFactory`. A test host wires ASP.NET Core Identity +
  `AddAuthMd`/`MapAuthMd` in-memory. `AuthMd.Client` is exercised through the
  **same** seam by pointing it at the factory's in-memory `HttpClient`, so one
  seam covers both packages end-to-end.
- **Headline test:** the full `service_auth` ceremony over real HTTP —
  `POST /agent/identity` → claim issued → (human step simulated by an
  authenticated HTTP call to the confirm operation as a seeded Identity User)
  → token minted → protected call succeeds with the right Scope → token and
  registration revocation take effect.
- **Modules tested:** `AuthMd.AspNetCore` and `AuthMd.Client`, both through the
  one seam. No default unit-test seam for the EF stores or OpenIddict glue; an
  isolated store test, if ever needed, is a deliberate second seam.
- **Prior art:** standard ASP.NET Core `WebApplicationFactory` integration
  testing with a test `DbContext` (e.g. EF Core in-memory or SQLite); this is
  the idiomatic .NET pattern a reviewer will expect.

## Out of Scope

- The `identity_assertion` ("Agent Verified") registration flow and the Trust
  List of external agent-identity-provider issuers (v2).
- The `anonymous` registration flow and pre-claim scopes (v2).
- A shipped claim-page UI / `AuthMd.AspNetCore.UI` companion package (ADR 0003).
- Multi-targeting older TFMs; v1 is net10.0 only.
- Publishing to NuGet.org at launch (GitHub first; NuGet once stabilized).
- The provider-driven events endpoint (SET delivery) beyond what registration
  revocation needs.

## Further Notes

- auth.md is built on OAuth 2.0: RFC 9728 (Protected Resource Metadata),
  RFC 8628-shaped Claim Ceremony, RFC 7523 (`jwt-bearer` exchange), RFC 7009
  (revocation).
- This is, as far as research found, the first .NET/NuGet implementation of
  auth.md — the novelty is the basis of the portfolio/LinkedIn narrative.
- Written artifacts (polished README with sequence diagram, and the LinkedIn
  post with screenshots/GIF) are produced at the end against the working,
  published package so every claim is backed by a real demo.
