using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Medzo.Auth.Application.DTOs;
using Medzo.Auth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
        Assert.StartsWith("I", result.User.StaffId);
        Assert.Contains("InventoryManager", result.User.Roles);

        var retrieved = await _client.GetFromJsonAsync<UserResponse>($"/api/users/{result.User.Id}");
        Assert.NotNull(retrieved);
        Assert.Equal("happy@example.com", retrieved.Email);
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
                options.UseInMemoryDatabase(_databaseName, _databaseRoot));

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
