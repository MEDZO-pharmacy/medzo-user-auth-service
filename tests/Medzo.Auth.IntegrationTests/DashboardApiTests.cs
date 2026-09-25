using System.Net;
using Xunit;

namespace Medzo.Auth.IntegrationTests;

public class DashboardApiTests : IClassFixture<AdminApiFactory>
{
    private readonly HttpClient _client;

    public DashboardApiTests(AdminApiFactory factory) => _client = factory.CreateClient();

    [Theory]
    [InlineData("Pharmacist", "/api/dashboard/pharmacist")]
    [InlineData("InventoryManager", "/api/dashboard/inventory")]
    [InlineData("Admin", "/api/dashboard/admin")]
    public async Task Dashboard_WithMatchingRole_ReturnsOk(string role, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Test-Role", role);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Pharmacist_CannotOpenAdminDashboard()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/dashboard/admin");
        request.Headers.Add("X-Test-Role", "Pharmacist");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
