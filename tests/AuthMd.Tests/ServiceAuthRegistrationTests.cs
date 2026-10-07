using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AuthMd.Tests;

public class ServiceAuthRegistrationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Service_auth_registration_issues_a_claim_ceremony()
    {
        using var client = factory.CreateClient();
        var sentAt = DateTimeOffset.UtcNow;

        using var response = await client.PostAsJsonAsync("/agent/identity", new
        {
            type = "service_auth",
            login_hint = "user@example.com",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var registration = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.StartsWith("reg_", registration.String("registration_id"));
        Assert.Equal("service_auth", registration.String("registration_type"));
        Assert.Equal("http://localhost/agent/identity/claim", registration.String("claim_url"));
        Assert.StartsWith("clm_", registration.String("claim_token"));
        Assert.Equal(["api.read", "api.write"], registration.Strings("post_claim_scopes"));

        var claimTokenExpires = registration.GetProperty("claim_token_expires").GetDateTimeOffset();
        Assert.InRange(claimTokenExpires, sentAt.AddMinutes(9), sentAt.AddMinutes(11));

        var claim = registration.GetProperty("claim");
        Assert.Matches(@"^\d{6}$", claim.String("user_code"));
        Assert.Equal(600, claim.GetProperty("expires_in").GetInt32());
        Assert.Equal(5, claim.GetProperty("interval").GetInt32());
        Assert.StartsWith("http://localhost/claim?claim_attempt_token=", claim.String("verification_uri"));
    }

    [Fact]
    public async Task Anonymous_registration_is_refused_as_not_enabled()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/agent/identity", new { type = "anonymous" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("anonymous_not_enabled", error.String("error"));
    }

    [Fact]
    public async Task Identity_assertion_registration_is_refused_because_no_issuer_is_trusted()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/agent/identity", new
        {
            type = "identity_assertion",
            assertion_type = "urn:ietf:params:oauth:token-type:id-jag",
            assertion = "eyJhbGciOiJub25lIn0.e30.",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("issuer_not_enabled", error.String("error"));
    }

    [Theory]
    [InlineData("""{ "type": "service_auth" }""")]
    [InlineData("""{ "type": "service_auth", "login_hint": "" }""")]
    [InlineData("""{ "type": "service_auth", "login_hint": "not-an-email" }""")]
    [InlineData("""{ "type": "service_auth", "login_hint": 42 }""")]
    [InlineData("""{ "type": "carrier_pigeon" }""")]
    [InlineData("{}")]
    [InlineData("not json")]
    public async Task Malformed_registration_is_refused_as_invalid_request(string body)
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            "/agent/identity",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_request", error.String("error"));
    }
}
