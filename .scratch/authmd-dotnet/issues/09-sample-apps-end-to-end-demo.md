# 09: Sample apps — runnable end-to-end demo

**What to build:** The recordable demo. A sample ASP.NET Core Identity web Service that publishes `auth.md`, hosts a reference claim/consent page, and exposes a protected API; plus a console Agent app using `AuthMd.Client`. Running both performs the whole ceremony — the artifact for the README GIF and the LinkedIn post.

**Blocked by:** 08.

**Status:** ready-for-agent

- [x] The sample Service publishes `/auth.md`, hosts a reference claim/consent page (Identity login + Scope display + confirm), and exposes a protected API
- [ ] The reference claim page concretely demonstrates the headless page contract
- [x] The console Agent app uses `AuthMd.Client` to complete the ceremony and call the protected API
- [x] Running both apps performs the whole ceremony end-to-end from a cold start

## Comments

- 2026-10-07: Both samples built and run for real.
  - **How to run:** `dotnet run --project samples/AuthMd.Sample.Service` (listens on `http://localhost:5125`), then in a second terminal `dotnet run --project samples/AuthMd.Sample.Agent`. The Agent prints a link and a 6-digit code; open the link, sign in as `demo@example.com` / `Passw0rd!`, enter the code. The Agent then lists the user's notes, adds one, lists again, and revokes its access token.
  - **What was verified:** the Service was started from a cold start and the real console Agent ran against it over real HTTP, exit code 0, with the expected output. The human was played by a script: it opened the Agent's link, was redirected to `/Account/Login`, signed in through the real login form (antiforgery token included), landed on `/claim`, then made the same two calls the page's script makes (`view`, `confirm`) with the session cookie.
  - **What was not verified:** the claim page's JavaScript has not been run in a browser, which is why the second box is open. The page is served and the two operations it calls work with the cookie, but nobody has yet typed a code into it. Open the link in a browser once before recording.
  - Sample Service: Identity on an in-memory SQLite database (every run starts clean), one seeded demo user, Razor Pages for home, login, logout and the claim page, and a notes API (`GET /api/notes` needs `notes.read`, `POST /api/notes` needs `notes.write`). Plain HTTP on purpose, so the console Agent does not depend on a trusted dev certificate; the OpenIddict HTTPS requirement is lifted in Development only. Keys are ephemeral.
  - The claim page is the page contract in about 60 lines of script: `GET /agent/identity/claim/view` to show what is being asked, `POST /agent/identity/claim/confirm` to approve, and a message for each error code.
  - One regression test covers the sample through `WebApplicationFactory`: it publishes `/auth.md`, sends `/claim` to the login page when nobody is signed in, and answers 401 on its API without a token.
  - No "my agents" page: the package cannot list a User's registrations yet (see ticket 06), so the sample has no way to revoke one from the UI.
