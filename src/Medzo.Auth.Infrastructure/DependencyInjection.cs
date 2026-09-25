using System.Text;
using System.Security.Claims;
using Medzo.Auth.Application.Interfaces;
using Medzo.Auth.Infrastructure.Authentication;
using Medzo.Auth.Infrastructure.Persistence;
using Medzo.Auth.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Medzo.Auth.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Secrets and deployment-specific connection details must come from
        // environment variables, user secrets, or another external provider.
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection is not configured. Set ConnectionStrings__DefaultConnection.");
        }

        var jwtSecret = configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(jwtSecret))
        {
            throw new InvalidOperationException(
                "JWT Secret is not configured. Set Jwt__Secret.");
        }

        // Database
        services.AddDbContext<AuthDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                b => b.MigrationsAssembly(typeof(AuthDbContext).Assembly.FullName)));

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IFeedbackRepository, FeedbackRepository>();
        services.AddScoped<IStaffInvitationRepository, StaffInvitationRepository>();

        // Authentication services
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();

        // JWT Authentication
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["Jwt:Issuer"],
                ValidAudience = configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSecret)),
                ClockSkew = TimeSpan.Zero
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (!Guid.TryParse(userIdValue, out var userId))
                    {
                        context.Fail("Invalid user identity.");
                        return;
                    }

                    var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                    var user = await users.GetByIdAsync(userId);
                    var hasStaffRole = user?.Roles.Any(role =>
                        role.Name is "Admin" or "Pharmacist" or "InventoryManager") == true;
                    if (user is null || !user.IsActive || !hasStaffRole)
                    {
                        context.Fail("The account is not permitted to access the staff website.");
                        return;
                    }

                    var tokenRoles = context.Principal!.FindAll(ClaimTypes.Role)
                        .Select(claim => claim.Value)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var currentRoles = user.Roles.Select(role => role.Name)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    if (!tokenRoles.SetEquals(currentRoles))
                        context.Fail("The account permissions have changed. Sign in again.");
                }
            };
        });

        return services;
    }
}
