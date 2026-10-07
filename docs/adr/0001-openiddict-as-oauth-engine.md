# 1. OpenIddict as the OAuth 2.0 engine

Date: 2026-10-06

## Status

Accepted

## Context

auth.md is a thin agent-registration layer on top of standard OAuth 2.0. A
compliant Service needs a `/oauth2/token` endpoint supporting the RFC 7523
`jwt-bearer` grant plus auth.md's custom `claim` grant, RFC 7009 token
revocation, and RFC 9728 Protected Resource Metadata. We must choose how those
OAuth primitives are provided.

Options considered:

- **OpenIddict** — free/open-source, integrates natively with ASP.NET Core
  Identity, already ships the token endpoint, custom grant handlers,
  revocation, and server metadata.
- **Hand-rolled minimal OAuth layer** — no dependency, full control, but we
  would be implementing token issuance, signing, and grant security ourselves.
- **Duende IdentityServer** — mature, but commercially licensed.

## Decision

Build `AuthMd.AspNetCore` **on top of OpenIddict**. OpenIddict provides the
OAuth 2.0 machinery; the package's value is the auth.md layer it adds on top
(the `auth.md` document, PRM/AS metadata, the agent registration + claim
endpoints, and the custom claim grant wired into OpenIddict's token endpoint).

## Consequences

- We inherit standards-correct, audited token/crypto handling instead of
  writing our own — the right call for a security library and a stronger
  portfolio signal than "rolled my own OAuth."
- We take a hard dependency on OpenIddict and must track its idioms (store
  abstractions, custom grant handlers) and version compatibility.
- auth.md's custom grant type and non-standard endpoints require extension
  work against OpenIddict's extensibility points rather than out-of-the-box
  configuration.
- Duende's licensing cost/complexity is avoided, keeping the package freely
  usable (MIT).
