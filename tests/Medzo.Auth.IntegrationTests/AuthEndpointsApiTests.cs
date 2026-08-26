using System.Net;
using System.Net.Http.Json;
using Medzo.Auth.Application.DTOs;
using Xunit;

namespace Medzo.Auth.IntegrationTests;

public class AuthEndpointsApiTests : IClassFixture<AdminApiFactory>
{
    private readonly HttpClient _client;

    public AuthEndpointsApiTests(AdminApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("P", "Pharmacist")]
    [InlineData("I", "InventoryManager")]
    public async Task Register_StaffIdPrefix_AssignsExpectedRole(string prefix, string expectedRole)
    {
        var unique = Guid.NewGuid().ToString("N");
        var staffId = $"{prefix}{unique[..10]}";
        await ApproveStaffIdAsync(staffId, expectedRole);
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterUserRequest
        {
            Username = $"role.{unique}",
            StaffId = staffId,
            Email = $"role.{unique}@example.com",
            Password = "Strong1!",
            ConfirmPassword = "Strong1!",
            FirstName = expectedRole,
            LastName = "User"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(session);
        Assert.Contains(expectedRole, session.User.Roles);
        Assert.StartsWith(prefix, session.User.StaffId);
    }

    [Fact]
    public async Task RegisterThenLogin_WithValidCredentials_ReturnsAuthenticatedSessions()
    {
        var registration = new RegisterUserRequest
        {
            Username = $"auth.user.{Guid.NewGuid():N}",
            StaffId = $"P{Guid.NewGuid():N}"[..12],
            Email = $"auth.{Guid.NewGuid():N}@example.com",
            Password = "Strong1!",
            ConfirmPassword = "Strong1!",
            FirstName = "Auth",
            LastName = "User"
        };
        await ApproveStaffIdAsync(registration.StaffId, "Pharmacist");

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registration);

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registered = await registerResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(registered);
        Assert.NotEmpty(registered.Token);
        Assert.Empty(registered.RefreshToken);
        Assert.Contains(registerResponse.Headers.GetValues("Set-Cookie"), value =>
            value.Contains("medzo.refresh=") && value.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Pharmacist", registered.User.Roles);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Identifier = registration.StaffId,
            Password = registration.Password
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loggedIn = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loggedIn);
        Assert.NotEmpty(loggedIn.Token);
        Assert.Equal(registration.Username, loggedIn.User.Username);
    }

    [Fact]
    public async Task RefreshThenRevoke_UsesHttpOnlyCookieAndRejectsRevokedSession()
    {
        var registration = new RegisterUserRequest
        {
            Username = $"refresh.user.{Guid.NewGuid():N}",
            StaffId = $"I{Guid.NewGuid():N}"[..12],
            Email = $"refresh.{Guid.NewGuid():N}@example.com",
            Password = "Strong1!",
            ConfirmPassword = "Strong1!",
            FirstName = "Refresh",
            LastName = "User"
        };
        await ApproveStaffIdAsync(registration.StaffId, "InventoryManager");
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registration);
        registerResponse.EnsureSuccessStatusCode();
        var registered = await registerResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(registered);

        var refreshResponse = await _client.PostAsync("/api/auth/refresh", null);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var rotated = await refreshResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(rotated);
        Assert.Empty(rotated.RefreshToken);

        var revokeResponse = await _client.PostAsync("/api/auth/revoke", null);
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        var revokedRefreshResponse = await _client.PostAsync("/api/auth/refresh", null);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedRefreshResponse.StatusCode);
    }

    [Fact]
    public async Task ReviewAndContactSubmissions_ArePersisted()
    {
        var reviewResponse = await _client.PostAsJsonAsync("/api/reviews", new ReviewRequest
        {
            Name = "Persistent Customer",
            CustomerType = "Regular Customer",
            Rating = 5,
            Comment = "This review remains available after another request."
        });
        Assert.Equal(HttpStatusCode.Created, reviewResponse.StatusCode);
        var savedReview = await reviewResponse.Content.ReadFromJsonAsync<ReviewResponse>();
        Assert.NotNull(savedReview);

        var reviews = await _client.GetFromJsonAsync<ReviewResponse[]>("/api/reviews");
        Assert.Contains(reviews!, review => review.Id == savedReview.Id);

        var contactResponse = await _client.PostAsJsonAsync("/api/contact", new ContactMessageRequest
        {
            Name = "Contact Customer",
            Email = "contact@example.com",
            Subject = "Medicine availability",
            Message = "Please let me know when this medicine is available."
        });
        Assert.Equal(HttpStatusCode.Accepted, contactResponse.StatusCode);
    }

    [Fact]
    public async Task Register_WithUnapprovedStaffId_IsRejected()
    {
        var unique = Guid.NewGuid().ToString("N");
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterUserRequest
        {
            Username = $"unapproved.{unique}", StaffId = $"P{unique[..10]}",
            Email = $"unapproved.{unique}@example.com", Password = "Strong1!",
            ConfirmPassword = "Strong1!", FirstName = "Unapproved", LastName = "Staff"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithAdminStaffId_IsRejected()
    {
        var unique = Guid.NewGuid().ToString("N");
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterUserRequest
        {
            Username = $"admin.signup.{unique}", StaffId = $"A{unique[..10]}",
            Email = $"admin.signup.{unique}@example.com", Password = "Strong1!",
            ConfirmPassword = "Strong1!", FirstName = "Admin", LastName = "Signup"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("start with P or I", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Login_AcceptsUsernameOrEmail(bool useEmail)
    {
        var user = await CreateManagedUserAsync("Pharmacist", "P");
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Identifier = useEmail ? user.Email : user.Username,
            Password = "Strong1!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithAdminDeactivatedAccount_IsRejected()
    {
        var user = await CreateManagedUserAsync("InventoryManager", "I");
        var statusResponse = await _client.PatchAsJsonAsync(
            $"/api/users/{user.Id}/status", new SetUserStatusRequest { IsActive = false });
        statusResponse.EnsureSuccessStatusCode();

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Identifier = user.StaffId!,
            Password = "Strong1!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
        Assert.Contains("Invalid credentials", await loginResponse.Content.ReadAsStringAsync());
    }

    private async Task ApproveStaffIdAsync(string staffId, string role)
    {
        var response = await _client.PostAsJsonAsync("/api/users/staff-invitations", new StaffInvitationRequest
        {
            StaffId = staffId,
            Role = role
        });
        response.EnsureSuccessStatusCode();
    }

    private async Task<UserResponse> CreateManagedUserAsync(string role, string prefix)
    {
        var unique = Guid.NewGuid().ToString("N");
        var response = await _client.PostAsJsonAsync("/api/users", new CreateUserRequest
        {
            Username = $"managed.{unique}", StaffId = $"{prefix}{unique[..10]}",
            Email = $"managed.{unique}@example.com", Password = "Strong1!",
            ConfirmPassword = "Strong1!", FirstName = "Managed", LastName = unique,
            Role = role
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateUserResponse>())!.User;
    }
}
