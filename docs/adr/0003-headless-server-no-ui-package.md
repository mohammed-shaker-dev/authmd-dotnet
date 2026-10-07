# 3. Headless server — no UI package, a documented page contract instead

Date: 2026-10-06

## Status

Accepted

## Context

The `service_auth` Claim Ceremony requires a Service-owned web page where the
human enters the `user_code`, authenticates via Identity, and confirms the
grant. Someone must provide that page. The obvious precedent is
`Microsoft.AspNetCore.Identity.UI`: ship a default, scaffoldable Razor UI as a
companion package so `AddAuthMd()` + the UI package "just works."

## Decision

`AuthMd.AspNetCore` is **headless**. `AddAuthMd(...)` + `MapAuthMd()` serve
`/auth.md`, the metadata, and the agent/token endpoints. The claim/consent
**page is the consumer's responsibility**, built against a documented page
contract. No `AuthMd.AspNetCore.UI` package is shipped. The sample app provides
a real, working page as reference.

## Consequences

- No bundled Razor assets, scaffolding tooling, or opinionated styling to
  maintain or version; the package stays small and UI-framework-agnostic
  (works with Razor Pages, MVC, Blazor, SPA, etc.).
- Consumers must build or copy a page before the flow works — a deliberately
  higher first-run cost than the Identity.UI pattern a reader might expect.
  This is mitigated by the documented contract and the sample page.
- The end-to-end test simulates the human step with an authenticated HTTP call
  to the claim-confirm endpoint rather than driving a page, which keeps the
  test seam at the HTTP boundary (see the v1 spec's testing decisions).
- Revisitable: a UI companion package can be added later without changing the
  headless core.
