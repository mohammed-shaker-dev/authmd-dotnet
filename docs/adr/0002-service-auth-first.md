# 2. Ship the `service_auth` flow first

Date: 2026-10-06

## Status

Accepted

## Context

auth.md defines three registration methods:

- `identity_assertion` ("Agent Verified") — the Agent presents a JWT from a
  trusted provider; requires a Trust List of external issuers and a third
  party to mint assertions.
- `service_auth` ("User Claimed") — the Agent has only the User's email; the
  human completes a Claim Ceremony on the Service's own login page.
- `anonymous` — pre-claim scopes, no User yet.

v1 is showcase-first and must be self-contained and demoable, and its headline
differentiator is the ASP.NET Core Identity integration.

## Decision

v1 implements **`service_auth` only**. The other two flows are designed-for —
the internal abstraction leaves room for them — but deferred to v2.

## Consequences

- The one flow we ship is the one that best exercises ASP.NET Core Identity:
  the human authenticates through the app's existing Identity login to
  complete the Claim Ceremony. That is exactly the story the project is meant
  to tell.
- The demo needs no third-party identity provider or Trust List, so the whole
  ceremony runs end-to-end with just our two sample apps.
- Services that only support external-IdP agents (`identity_assertion`) are
  not yet served; "full protocol coverage" becomes the v2 headline.
- The Trust List concept is defined in the glossary but unused in v1 code.
