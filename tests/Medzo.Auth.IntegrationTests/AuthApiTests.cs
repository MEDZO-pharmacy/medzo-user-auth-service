using System.Net;
using Xunit;

namespace Medzo.Auth.IntegrationTests;

public class AuthApiTests : IClassFixture<AdminApiFactory>
{
    private readonly HttpClient _client;

    public AuthApiTests(AdminApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_ShouldReturn200()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EvaluateSession_WithAuthenticatedUser_ReturnsNoContent()
    {
        var response = await _client.GetAsync("/api/auth/session");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
