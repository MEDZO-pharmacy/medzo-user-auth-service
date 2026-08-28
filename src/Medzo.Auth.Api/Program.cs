using Medzo.Auth.Api.Configuration;
using Medzo.Auth.Api.ExceptionHandling;
using Medzo.Auth.Api.Extensions;
using Medzo.Auth.Infrastructure;

DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DatabaseExceptionHandler>();
builder.Services.AddHealthChecks();

const string frontendCorsPolicy = "Frontend";
var frontendOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy(frontendCorsPolicy, policy =>
    {
        policy.WithOrigins(frontendOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var httpsPort = builder.Configuration.GetValue<int?>("HttpsRedirection:HttpsPort");
if (httpsPort.HasValue)
{
    builder.Services.AddHttpsRedirection(options => options.HttpsPort = httpsPort.Value);
}

// Register application & infrastructure services
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseExceptionHandler();
app.UseCors(frontendCorsPolicy);

if (httpsPort.HasValue)
{
    app.UseHttpsRedirection();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
