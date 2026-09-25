using FluentValidation;
using Medzo.Auth.Application.Interfaces;
using Medzo.Auth.Application.Services;

namespace Medzo.Auth.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Application services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IFeedbackService, FeedbackService>();

        // FluentValidation — auto-register all validators from Application assembly
        services.AddValidatorsFromAssemblyContaining<IAuthService>();

        return services;
    }
}
