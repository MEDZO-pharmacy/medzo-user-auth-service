using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Medzo.Auth.Application.DTOs;
using Medzo.Auth.Domain.Entities;
using Medzo.Auth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Medzo.Auth.IntegrationTests;

public class AdminUserCreationApiTests : IClassFixture<AdminApiFactory>
{
    private readonly HttpClient _client;

    public AdminUserCreationApiTests(AdminApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_WithValidUser_PersistsRoleAndReturnsCreated()
    {
        var response = await _client.PostAsJsonAsync("/api/users", Request(
            "happy.path", "happy@example.com", "Happy", "Path"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateUserResponse>();
        Assert.NotNull(result);
        Assert.Equal("happy.path", result.User.Username);
        Assert.True(result.User.UserNumber > 0);
        Assert.Equal(result.User.UserNumber.ToString("D3"), result.User.UserCode);
        Assert.StartsWith("I", result.User.StaffId);
        Assert.Contains("InventoryManager", result.User.Roles);

        var retrieved = await _client.GetFromJsonAsync<UserResponse>($"/api/users/{result.User.Id}");
        Assert.NotNull(retrieved);
        Assert.Equal("happy@example.com", retrieved.Email);
        Assert.Equal(result.User.UserNumber, retrieved.UserNumber);
        Assert.Equal(result.User.UserCode, retrieved.UserCode);
    }

    [Fact]
    public async Task Create_MultipleUsers_AssignsConsecutiveUserNumbers()
    {
        var firstResponse = await _client.PostAsJsonAsync("/api/users", Request(
            $"sequence.one.{Guid.NewGuid():N}", $"sequence.one.{Guid.NewGuid():N}@example.com", "Sequence", "One"));
        var secondResponse = await _client.PostAsJsonAsync("/api/users", Request(
            $"sequence.two.{Guid.NewGuid():N}", $"sequence.two.{Guid.NewGuid():N}@example.com", "Sequence", "Two"));

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);

        var first = (await firstResponse.Content.ReadFromJsonAsync<CreateUserResponse>())!.User;
        var second = (await secondResponse.Content.ReadFromJsonAsync<CreateUserResponse>())!.User;

        Assert.Equal(first.UserNumber + 1, second.UserNumber);
        Assert.Equal(first.UserNumber.ToString("D3"), first.UserCode);
        Assert.Equal(second.UserNumber.ToString("D3"), second.UserCode);
    }

    [Fact]
    public async Task Create_WithInvalidRequiredField_ReturnsValidationAndDoesNotPartiallySave()
    {
        var invalid = Request("validation.user", "validation@example.com", "", "User");
        var rejected = await _client.PostAsJsonAsync("/api/users", invalid);

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var body = await rejected.Content.ReadAsStringAsync();
        Assert.Contains("First name is required.", body);

        invalid.FirstName = "Validation";
        var corrected = await _client.PostAsJsonAsync("/api/users", invalid);
        Assert.Equal(HttpStatusCode.Created, corrected.StatusCode);
    }

    [Fact]
    public async Task Create_WithMatchingName_WarnsThenAllowsConfirmedSave()
    {
        var first = await _client.PostAsJsonAsync("/api/users", Request(
            "duplicate.one", "duplicate.one@example.com", "Similar", "Person"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var candidate = Request(
            "duplicate.two", "duplicate.two@example.com", "Similar", "Person");
        var warning = await _client.PostAsJsonAsync("/api/users", candidate);

        Assert.Equal(HttpStatusCode.Conflict, warning.StatusCode);
        var warningBody = await warning.Content.ReadFromJsonAsync<PotentialDuplicateResponse>();
        Assert.NotNull(warningBody);
        Assert.Equal("potential_duplicate", warningBody.Code);
        Assert.True(warningBody.ConfirmationRequired);
        Assert.Single(warningBody.Duplicates);

        candidate.ConfirmPotentialDuplicate = true;
        var confirmed = await _client.PostAsJsonAsync("/api/users", candidate);
        Assert.Equal(HttpStatusCode.Created, confirmed.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutAdminRole_IsForbidden()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/users")
        {
            Content = JsonContent.Create(Request(
                "forbidden.user", "forbidden@example.com", "Forbidden", "User"))
        };
        request.Headers.Add("X-Test-Role", "InventoryManager");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_AdminAccountThroughApi_IsRejected()
    {
        var request = Request("api.admin", "api.admin@example.com", "API", "Admin");
        request.StaffId = "A9001";
        request.Role = "Admin";

        var response = await _client.PostAsJsonAsync("/api/users", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("provisioned manually", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Approve_AdminStaffIdThroughApi_IsRejected()
    {
        var response = await _client.PostAsJsonAsync("/api/users/staff-invitations", new StaffInvitationRequest
        {
            StaffId = "A9002",
            Role = "Admin"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("provisioned manually", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UpdateManagedUser_ChangesDetailsAndRole()
    {
        var createdResponse = await _client.PostAsJsonAsync("/api/users", Request(
            $"edit.{Guid.NewGuid():N}", $"edit.{Guid.NewGuid():N}@example.com", "Before", "Edit"));
        var created = (await createdResponse.Content.ReadFromJsonAsync<CreateUserResponse>())!.User;

        var response = await _client.PutAsJsonAsync($"/api/users/{created.Id}/managed", new UpdateManagedUserRequest
        {
            Username = $"edited.{Guid.NewGuid():N}",
            StaffId = $"P{Guid.NewGuid():N}"[..12],
            Email = $"edited.{Guid.NewGuid():N}@example.com",
            FirstName = "After",
            LastName = "Edit",
            Role = "Pharmacist"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(updated);
        Assert.Equal("After", updated.FirstName);
        Assert.StartsWith("P", updated.StaffId);
        Assert.Equal(["Pharmacist"], updated.Roles);
    }

    [Fact]
    public async Task SetStatus_DeactivatesAndPermanentlyReservesManagedUser()
    {
        var createdResponse = await _client.PostAsJsonAsync("/api/users", Request(
            $"deactivate.{Guid.NewGuid():N}", $"deactivate.{Guid.NewGuid():N}@example.com", "Deactivate", "User"));
        var created = (await createdResponse.Content.ReadFromJsonAsync<CreateUserResponse>())!.User;

        var response = await _client.PatchAsJsonAsync(
            $"/api/users/{created.Id}/status", new SetUserStatusRequest { IsActive = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(updated);
        Assert.False(updated.IsActive);

        var deactivatedAccount = await _client.GetFromJsonAsync<UserResponse>($"/api/users/{created.Id}");
        Assert.NotNull(deactivatedAccount);
        Assert.False(deactivatedAccount.IsActive);

        var dashboard = await _client.GetFromJsonAsync<AdminDashboardResponse>("/api/dashboard/admin");
        Assert.NotNull(dashboard);
        var dashboardAccount = Assert.Single(dashboard.Users, user => user.Id == created.Id);
        Assert.False(dashboardAccount.IsActive);

        var users = await _client.GetFromJsonAsync<UserResponse[]>("/api/users");
        Assert.NotNull(users);
        var listedAccount = Assert.Single(users, user => user.Id == created.Id);
        Assert.False(listedAccount.IsActive);

        Assert.NotNull(created.StaffId);
        var invitations = await _client.GetFromJsonAsync<StaffInvitationResponse[]>(
            "/api/users/staff-invitations");
        Assert.NotNull(invitations);
        var reservation = Assert.Single(invitations, item => item.StaffId == created.StaffId);
        Assert.True(reservation.IsClaimed);

        var approval = await _client.PostAsJsonAsync("/api/users/staff-invitations", new StaffInvitationRequest
        {
            StaffId = created.StaffId!,
            Role = "InventoryManager"
        });
        Assert.Equal(HttpStatusCode.Conflict, approval.StatusCode);

        var signup = await _client.PostAsJsonAsync("/api/auth/register", new RegisterUserRequest
        {
            Username = $"reuse.{Guid.NewGuid():N}",
            StaffId = created.StaffId!,
            Email = $"reuse.{Guid.NewGuid():N}@example.com",
            Password = "Strong1!",
            ConfirmPassword = "Strong1!",
            FirstName = "Reuse",
            LastName = "Blocked"
        });
        Assert.Equal(HttpStatusCode.BadRequest, signup.StatusCode);
        Assert.Contains("already been used", await signup.Content.ReadAsStringAsync());

        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Identifier = created.StaffId!,
            Password = "Strong1!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    private sealed record AdminDashboardResponse(UserResponse[] Users, int TotalUsers);

    private static CreateUserRequest Request(
        string username, string email, string firstName, string lastName) => new()
    {
        Username = username,
        StaffId = $"I{Guid.NewGuid():N}"[..12],
        Email = email,
        Password = "Strong1!",
        ConfirmPassword = "Strong1!",
        FirstName = firstName,
        LastName = lastName,
        Role = "InventoryManager"
    };
}

public class AdminApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"admin-user-tests-{Guid.NewGuid()}";
    private readonly InMemoryDatabaseRoot _databaseRoot = new();
    private readonly InMemoryUserNumberInterceptor _userNumbers = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=(localdb)\\mssqllocaldb;Database=MedzoAuthTests;Trusted_Connection=True;",
                ["Jwt:Secret"] = "integration-test-secret-that-is-not-used-outside-tests"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AuthDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AuthDbContext>>();
            services.AddDbContext<AuthDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName, _databaseRoot)
                    .AddInterceptors(_userNumbers));

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationSchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.AuthenticationSchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.AuthenticationSchemeName, _ => { });

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.EnsureCreated();
        });
    }
}

public class InMemoryUserNumberInterceptor : SaveChangesInterceptor
{
    private int _nextUserNumber;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            foreach (var entry in eventData.Context.ChangeTracker.Entries<User>()
                         .Where(entry => entry.State == EntityState.Added && entry.Entity.UserNumber == 0))
            {
                entry.Entity.UserNumber = Interlocked.Increment(ref _nextUserNumber);
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationSchemeName = "IntegrationTest";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers.TryGetValue("X-Test-Role", out var requestedRole)
            ? requestedRole.ToString()
            : "Admin";
        var userId = Request.Headers.TryGetValue("X-Test-User-Id", out var requestedUserId)
            ? requestedUserId.ToString()
            : Guid.NewGuid().ToString();
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, "integration-admin"),
            new Claim(ClaimTypes.Role, role)
        }, AuthenticationSchemeName);
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identity), AuthenticationSchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
