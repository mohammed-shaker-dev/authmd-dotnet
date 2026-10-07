# 07: AuthMd.Client — discovery

**What to build:** The Agent SDK's ability to understand any auth.md Service. `AuthMd.Client` fetches `/auth.md`, performs the two-hop PRM + authorization-server metadata walk, and figures out that `service_auth` is the applicable method and where its endpoints are. This runs on a parallel track to the server tickets.

**Blocked by:** 02.

**Status:** ready-for-agent

- [x] `AuthMd.Client` fetches `/auth.md` and performs the two-hop PRM + AS metadata discovery (via `WWW-Authenticate` or the well-known path)
- [x] It identifies `service_auth` as the applicable registration method and extracts the relevant endpoints and supported Scopes
- [x] Verified against the in-memory server seam
- [x] Buildable in parallel with server tickets 03–06 (depends only on the discovery documents from 02)

## Comments

- 2026-10-07: Implemented test-first through the HTTP seam (`tests/AuthMd.Tests/ClientDiscoveryTests.cs`): `new AuthMdClient(httpClient).DiscoverAsync(url)` returns a `DiscoveredService`.
  - `DiscoverAsync` takes the Service's base URL or a protected API URL. It probes the URL; a 401 with `WWW-Authenticate: Bearer resource_metadata="…"` is followed, otherwise it falls back to `/.well-known/oauth-protected-resource` on the same host. Then `authorization_servers[0]` + `/.well-known/oauth-authorization-server`, then the `agent_auth.skill` document, exposed as `Instructions`.
  - The `WWW-Authenticate` path is covered but not *forced* by a test: against this server the header points at the same well-known URL the fallback would use, so the two routes are indistinguishable over HTTP. It only matters for a Service that publishes its PRM elsewhere.
  - Failures raise `AuthMdDiscoveryException` naming the document. One test covers a host serving no metadata; the other branches (no authorization server listed, no `agent_auth` block, missing endpoint) were written without tests because this server cannot produce those documents.
  - Not done: the client does not check that the metadata's `issuer` matches the authorization server it fetched from (RFC 8414 §3.3), nor that the PRM's `resource` matches the URL asked about (RFC 9728 §3.3). Both are mix-up defences worth adding with a test host that can serve mismatched documents.
  - `AuthMd.Client` has no package dependencies.
