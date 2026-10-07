# Context: authmd-dotnet

The canonical vocabulary for this project. These terms mean exactly what is
written here — use them precisely in code, tests, docs, commits, and issues.
This file is a glossary, not a spec and not a place for implementation detail.

## Glossary

### Service
An ASP.NET Core application that uses ASP.NET Core Identity and has been made
auth.md-compliant by the `AuthMd.AspNetCore` package. It publishes `/auth.md`
and the OAuth metadata, and accepts agent registrations. "The service" is
always the thing being protected, never the agent.

### Agent
An AI agent that acts **on behalf of** a human User. An Agent is never an
account in its own right; it is always tied to a User through an
[Agent Registration](#agent-registration).

### User
An ASP.NET Core Identity account — a row in `AspNetUsers`. The human on whose
behalf an Agent acts. The User authenticates through the Service's existing
Identity login during the [Claim Ceremony](#claim-ceremony).

### Agent Registration
The persistent relationship between an Agent and the Service, on behalf of a
User. It carries the granted [Scopes](#scope) and is **the unit of
revocation**: revoking a registration invalidates every credential derived
from it. Distinct from an individual [Access Token](#access-token), which can
be revoked without ending the registration.

### Claim Ceremony
The `service_auth` user-verification process (RFC 8628-shaped). The Agent
presents the User's email; the Service issues a short-lived `user_code` and a
`claim_token`. The Agent surfaces the `user_code` to the human; the human
authenticates on a **Service-owned page** and confirms. State moves
`pending → claimed` (or expires). The Service never emails the code — it
travels Agent → human, and the human enters it on the Service's page.

### Identity Assertion
A Service-signed JWT representing a completed Agent Registration (agent acting
for a User). It is exchanged at the token endpoint for Access Tokens and can be
re-exchanged until it expires or is revoked. (Minted after a successful Claim
Ceremony in v1; the `identity_assertion` *registration flow* that accepts a
third-party assertion up front is v2.)

### Access Token
A short-lived OAuth 2.0 bearer token minted from an Identity Assertion via the
RFC 7523 `jwt-bearer` grant. Sent as `Authorization: Bearer <token>` to call
protected Service APIs. No refresh token is issued — a new Access Token is
obtained by re-exchanging the Identity Assertion.

### Scope
A named permission grant. Advertised in the Protected Resource Metadata and
attached to an Agent Registration and the Access Tokens derived from it.

### Protected Resource Metadata (PRM)
The RFC 9728 document at `/.well-known/oauth-protected-resource` describing the
resource, its authorization server(s), and supported scopes. Agents discover it
via the two-hop walk starting from a `WWW-Authenticate` header or the
well-known path.

### Trust List
A Service-maintained registry of agent-identity-provider issuers whose
assertions the Service accepts up front. Only relevant to the
`identity_assertion` registration flow — **out of scope for v1**, defined here
so the term is fixed.

## Related

- Decisions are recorded in `docs/adr/` and the v1 feature spec in `docs/specs/`.
  Both are working documents kept alongside the code locally; they are not published in this repository.
