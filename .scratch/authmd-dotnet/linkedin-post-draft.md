# LinkedIn post draft

Two things to fill in before posting: `[REPO LINK]`, and the GIF (shot list at the bottom).
LinkedIn does not render Markdown, so the post below is plain text. Paste it as-is.

---

AI agents can now sign up for a web app on someone's behalf. I built the .NET side of that.

The problem: an agent that needs to act for you inside an app has two options today. Drive a browser through the sign-up form, or be handed a long-lived API key. Neither is good.

WorkOS published an open protocol for this called auth.md. A service puts a Markdown file at its own domain that tells an agent how to register. The agent follows it, and a human approves.

I couldn't find a .NET implementation, so I wrote one: authmd-dotnet, two packages for .NET 10.

How it works:

1. The agent tells your app "I'd like to act for user@example.com".
2. Your app gives the agent a 6-digit code and a link. It never emails the code.
3. The user opens the link, signs in the way they always do, and types the code on your page.
4. The agent, which has been polling, gets a short-lived token limited to what it was allowed to do.

The user approves on the real site with their real login. The agent never sees a password, and cutting the agent off later doesn't mean changing one.

For an ASP.NET Core Identity app it comes down to AddAuthMd() and MapAuthMd(), plus one page you own where the user enters the code. Underneath it is OpenIddict, so the token handling is not home-made.

What I'd point a reviewer at:

- 55 tests, all driven over HTTP. One runs the entire flow with the agent SDK on one side and the server on the other.
- A bug those tests caught: access tokens and the agent's longer-lived credential were signed with the same key, and for a moment the server accepted one in place of the other.
- A README that lists what it doesn't do yet. This is an alpha, and it implements one of the protocol's three registration methods.

Code, and a demo you can run in two terminals: [REPO LINK]

If you run an Identity app and have thought about letting agents in, I'd like to hear what would stop you.

#dotnet #aspnetcore #oauth #aiagents

---

## The GIF

Attach it to the post; LinkedIn shows it above the text. Aim for 20–30 seconds, two windows side by side: terminal on the left, browser on the right.

1. Terminal: `dotnet run --project samples/AuthMd.Sample.Agent` (the service is already running in a tab you don't show). Hold on the box with the 6-digit code.
2. Browser: open the link the agent printed. It lands on the sign-in page.
3. Sign in as `demo@example.com` / `Passw0rd!`. The "Approve an agent" page appears, listing "Read your notes" and "Add notes".
4. Type the code, click Approve. Hold on "Approved."
5. Terminal: the agent prints "Approved.", the two notes, then the third note it added.

Before recording, open the claim page in a browser once on its own. Its script has not been exercised in a real browser yet (see ticket 09), so that is the step most likely to need a fix.

If a GIF is too heavy, two screenshots do the job: the terminal showing the code, and the approval page.

## Notes on the wording

- "I couldn't find a .NET implementation" is as far as the claim goes. The spec's research found none, but nobody has proved there isn't one, so the post does not say "the first".
- Every number and behaviour in the post is true of the repository as committed: 55 tests, .NET 10, OpenIddict, `service_auth` only, alpha, not on NuGet.org.
- The token-confusion bug is real and is described in ticket 05.
- The packages are not published, so the post links to the repository, not to NuGet.
