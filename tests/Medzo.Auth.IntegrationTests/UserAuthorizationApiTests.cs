using System.Net;
using System.Net.Http.Json;
using Medzo.Auth.Application.DTOs;
using Xunit;

namespace Medzo.Auth.IntegrationTests;

public class UserAuthorizationApiTests : IClassFixture<AdminApiFactory>
{
    private readonly HttpClient _client;

    public UserAuthorizationApiTests(AdminApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetById_AsOwner_ReturnsUser()
    {
        var user = await CreateUserAsync("owner.read");
        using var request = StaffRequest(HttpMethod.Get, $"/api/users/{user.Id}", user.Id);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AsDifferentUser_ReturnsForbidden()
    {
        var user = await CreateUserAsync("other.read");
        using var request = StaffRequest(HttpMethod.Get, $"/api/users/{user.Id}", Guid.NewGuid());

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsDifferentUser_ReturnsForbidden()
    {
        var user = await CreateUserAsync("other.update");
        using var request = StaffRequest(HttpMethod.Put, $"/api/users/{user.Id}", Guid.NewGuid());
        request.Content = JsonContent.Create(new RegisterUserRequest
        {
            Username = "unauthorized.update",
            StaffId = user.StaffId!,
            Email = "unauthorized.update@example.com",
            Password = "Strong1!",
            ConfirmPassword = "Strong1!",
            FirstName = "Unauthorized",
            LastName = "Update"
        });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithInvalidUsername_ReturnsValidationProblem()
    {
        var user = await CreateUserAsync("invalid.update");
        using var request = StaffRequest(HttpMethod.Put, $"/api/users/{user.Id}", user.Id);
        request.Content = JsonContent.Create(UpdateRequest("invalid username", "valid@example.com", user.StaffId!));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithAnotherUsersEmail_ReturnsConflict()
    {
        var first = await CreateUserAsync("conflict.first");
        var second = await CreateUserAsync("conflict.second");
        using var request = StaffRequest(HttpMethod.Put, $"/api/users/{first.Id}", first.Id);
        request.Content = JsonContent.Create(UpdateRequest("conflict.first", second.Email, first.StaffId!));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private async Task<UserResponse> CreateUserAsync(string username)
    {
        var response = await _client.PostAsJsonAsync("/api/users", new CreateUserRequest
        {
            Username = username,
            StaffId = $"I{Guid.NewGuid():N}"[..12],
            Email = $"{username}@example.com",
            Password = "Strong1!",
            ConfirmPassword = "Strong1!",
            FirstName = username,
            LastName = "Authorization",
            Role = "InventoryManager"
        });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateUserResponse>();
        return result!.User;
    }

    private static HttpRequestMessage StaffRequest(HttpMethod method, string path, Guid userId)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Test-Role", "InventoryManager");
        request.Headers.Add("X-Test-User-Id", userId.ToString());
        return request;
    }

    private static RegisterUserRequest UpdateRequest(string username, string email, string staffId) => new()
    {
        Username = username,
        StaffId = staffId,
        Email = email,
        Password = "Strong1!",
        ConfirmPassword = "Strong1!",
        FirstName = "Updated",
        LastName = "User"
    };
}
