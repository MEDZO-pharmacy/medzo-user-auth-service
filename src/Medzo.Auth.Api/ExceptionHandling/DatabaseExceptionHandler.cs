using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Medzo.Auth.Api.ExceptionHandling;

public sealed class DatabaseExceptionHandler : IExceptionHandler
{
    private readonly ILogger<DatabaseExceptionHandler> _logger;

    public DatabaseExceptionHandler(ILogger<DatabaseExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!ContainsDatabaseException(exception))
            return false;

        _logger.LogError(exception, "A database operation failed while processing {Path}.",
            httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "Service temporarily unavailable",
            Detail = "The data service could not complete the request. Please try again later."
        }, cancellationToken);
        return true;
    }

    private static bool ContainsDatabaseException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateException or SqlException)
                return true;
        }

        return false;
    }
}
