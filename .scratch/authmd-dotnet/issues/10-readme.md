# 10: README — sequence diagram + quickstart + "why auth.md"

**What to build:** The repo's front door, written against the working demo so every claim is true. A reader understands what auth.md is, why this package exists, and how to use it within a couple of minutes.

**Blocked by:** 09.

**Status:** ready-for-agent

- [x] README includes a sequence diagram of the `service_auth` Claim Ceremony
- [x] Quickstart: install, `AddAuthMd`/`MapAuthMd`, build the claim page (page contract), run the demo
- [x] "Why auth.md" narrative and explicit v1 scope (`service_auth` only; the other flows noted as v2)
- [ ] All claims verified against the working demo from ticket 09

## Comments

- 2026-10-07: `README.md` written at the repo root and packed into both NuGet packages as their readme.
  - Verified against the code and the ticket 09 run: the endpoints, option names and defaults, error codes and statuses, the SDK calls in the agent quickstart (they mirror the sample Agent, which ran), the demo commands and credentials, and the demo output (pasted from the real run).
  - **Not verified, which is why the last box is open:**
    - The production key lines (`AddSigningCertificate` / `AddEncryptionCertificate`) were not run; the samples and tests use ephemeral keys.
    - "Without them OpenIddict fails with a configuration error" and "OpenIddict refuses plain-HTTP requests" are OpenIddict's documented behaviour; neither was reproduced here, because the hosts were configured correctly from the start.
    - `dotnet ef migrations add AddAuthMd` was not run; the samples and tests create the schema with `EnsureCreated`.
    - The Mermaid diagram was not rendered.
    - The claim page has still not been used in a browser (see ticket 09).
  - There is no "install from NuGet" step because the packages are not published; the README says so and points at project reference or a local pack.
  - No repository URL, badges or GIF yet: the repo has not been pushed, and the recording is ticket 11's material.
  - The README lists the known limits of v1 in one place, collected from the comments on tickets 02–08.
