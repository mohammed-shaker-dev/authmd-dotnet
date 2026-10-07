namespace AuthMd.AspNetCore;

/// <summary>
/// Renders the <c>/auth.md</c> document: the instructions an Agent follows to register with the Service.
/// </summary>
internal static class AuthMdDocument
{
    public static string Render(ServiceUrls urls, AuthMdOptions options)
    {
        var service = options.ResourceName ?? "This service";
        var scopesJson = string.Join(", ", options.Scopes.Select(scope => $"\"{scope}\""));
        var scopesParam = string.Join(' ', options.Scopes);

        return $$"""
            # auth.md

            You are an agent. {{service}} supports **agentic registration**: discover → register → claim → exchange for an access_token → call API → handle revocation. Follow the steps in order; do not skip ahead.

            ## Step 1 — Discover

            Discovery is two hops — you may have already done this.

            A 401 from the API carries a `WWW-Authenticate` header with the Protected Resource Metadata (PRM) URL:

            ```http
            HTTP/1.1 401 Unauthorized
            WWW-Authenticate: Bearer resource_metadata="{{urls.ProtectedResourceMetadata}}"
            ```

            ### 1a. Fetch the Protected Resource Metadata

            ```http
            GET {{urls.ProtectedResourceMetadata}}
            ```

            - `resource` — the canonical URL of the API you're trying to call.
            - `resource_name` / `resource_logo_uri` — display name and logo. Surface these to the user when asking for consent.
            - `authorization_servers` — base URLs of the authorization server(s) for this resource.
            - `scopes_supported` — scopes the resource server understands.
            - `bearer_methods_supported` — how you send the access_token (`"header"` = `Authorization: Bearer …`).

            ### 1b. Fetch the Authorization Server metadata

            ```http
            GET {{urls.AuthorizationServerMetadata}}
            ```

            - `token_endpoint` — where you poll the claim ceremony (Step 4) and exchange the identity assertion (Step 5).
            - `revocation_endpoint` — where you POST to revoke an access_token.
            - `agent_auth.identity_endpoint` — where you POST to register (Step 3).
            - `agent_auth.identity_types_supported` — the registration methods this service accepts.

            ## Step 2 — Pick a method

            This service accepts one registration method: **service_auth**. You need the user's email. A claim ceremony is required.

            ## Step 3 — Register

            Before registering, surface the service's `resource_name` and the scopes you'll be acting under, and confirm with the user.

            ```http
            POST {{urls.IdentityEndpoint}}
            Content-Type: application/json

            {
              "type": "service_auth",
              "login_hint": "user@example.com"
            }
            ```

            Response (200):

            ```json
            {
              "registration_id": "reg_...",
              "registration_type": "service_auth",
              "claim_url": "{{urls.ClaimEndpoint}}",
              "claim_token": "clm_...",
              "claim_token_expires": "2026-05-21T17:31:25.994Z",
              "post_claim_scopes": [{{scopesJson}}],
              "claim": {
                "user_code": "123456",
                "expires_in": 600,
                "verification_uri": "{{urls.Issuer}}...",
                "interval": 5
              }
            }
            ```

            There is no `identity_assertion` yet — it arrives when the claim ceremony completes. `claim_token` is returned exactly once — hold it in memory for the duration of the ceremony; do not persist it past Step 4.

            ## Step 4 — Claim ceremony

            The goal: a signed-in user confirms the `user_code` **you supply them**. The code travels from you → user, never from the service → user; the user authenticates to the service and types the code into a page the service owns.

            ### 4a. Hand off to the user

            Surface `claim.verification_uri` and `claim.user_code` to the user in a single message:

            > Open this link, sign in, and enter this code: **123456**

            Be explicit that the code goes into the page they land on after signing in — not back to you.

            ### 4b. Poll for completion

            ```http
            POST {{urls.TokenEndpoint}}
            Content-Type: application/x-www-form-urlencoded

            grant_type={{GrantTypes.Claim}}
            &claim_token=<clm_...>
            ```

            While waiting:

            ```json
            { "error": "authorization_pending", "error_description": "..." }
            ```

            On success — a standard OAuth token response plus an `identity_assertion`:

            ```json
            {
              "access_token": "<access_token>",
              "token_type": "Bearer",
              "expires_in": 3600,
              "scope": "{{scopesParam}}",
              "identity_assertion": "<service-signed JWT>",
              "assertion_expires": "2026-05-21T18:31:25.994Z"
            }
            ```

            Use `access_token` immediately; keep `identity_assertion` for Step 5. Honor `claim.interval` (seconds) between polls; on `slow_down` add at least 5 seconds. If the poll returns `expired_token`, the user did not finish in time — restart at Step 3.

            ## Step 5 — Exchange the assertion

            ```http
            POST {{urls.TokenEndpoint}}
            Content-Type: application/x-www-form-urlencoded

            grant_type={{GrantTypes.JwtBearer}}
            &assertion=<identity_assertion>
            &resource={{urls.Resource}}
            ```

            Response (200):

            ```json
            {
              "access_token": "<access_token>",
              "token_type": "Bearer",
              "expires_in": 3600,
              "scope": "{{scopesParam}}"
            }
            ```

            The same `identity_assertion` can be re-used to mint additional access_tokens until it expires. If the token endpoint returns `invalid_grant`, the assertion is expired or revoked — restart at Step 3.

            ## Step 6 — Use the access_token

            ```http
            GET /api/some-resource
            Authorization: Bearer <access_token>
            ```

            When the access_token expires, repeat Step 5 with the same `identity_assertion`. There is no OAuth refresh_token in this flow.

            ## Errors

            | Code                      | Where                           | What to do                                                        |
            | ------------------------- | ------------------------------- | ----------------------------------------------------------------- |
            | `invalid_request`         | identity endpoint               | Body shape is wrong or `login_hint` is missing. Fix the input.    |
            | `anonymous_not_enabled`   | identity endpoint               | This service doesn't accept anonymous. Use `service_auth`.        |
            | `issuer_not_enabled`      | identity endpoint               | This service doesn't accept identity assertions. Use `service_auth`. |
            | `authorization_pending`   | token endpoint (claim grant)    | The user hasn't completed the ceremony yet. Wait `interval`; retry. |
            | `slow_down`               | token endpoint (claim grant)    | Polling too fast. Add at least 5s to your interval and retry.     |
            | `expired_token`           | token endpoint (claim grant)    | The claim window closed. Restart at Step 3.                       |
            | `invalid_grant`           | token endpoint                  | Assertion expired or revoked. Restart at Step 3.                  |
            | `unsupported_grant_type`  | token endpoint                  | `grant_type` is not one of the two supported values.              |

            ## Revocation

            - **Credential** — POST `token=<access_token>&token_type_hint=access_token` (form-encoded) to `{{urls.RevocationEndpoint}}` to revoke one access_token. Your `identity_assertion` is intact; repeat Step 5 for a fresh one.
            - **Registration** — the service or the user can revoke your registration. You discover it when the token endpoint returns `invalid_grant` — restart at Step 3.

            On a 401 for a previously-working access_token: try Step 5 once. If that returns `invalid_grant`, restart at Step 3.

            """;
    }
}
