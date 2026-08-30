FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the solution and project files
COPY ["Medzo.Auth.sln", "./"]
COPY ["src/Medzo.Auth.Api/Medzo.Auth.Api.csproj", "src/Medzo.Auth.Api/"]
COPY ["src/Medzo.Auth.Application/Medzo.Auth.Application.csproj", "src/Medzo.Auth.Application/"]
COPY ["src/Medzo.Auth.Domain/Medzo.Auth.Domain.csproj", "src/Medzo.Auth.Domain/"]
COPY ["src/Medzo.Auth.Infrastructure/Medzo.Auth.Infrastructure.csproj", "src/Medzo.Auth.Infrastructure/"]
COPY ["tests/Medzo.Auth.UnitTests/Medzo.Auth.UnitTests.csproj", "tests/Medzo.Auth.UnitTests/"]
COPY ["tests/Medzo.Auth.IntegrationTests/Medzo.Auth.IntegrationTests.csproj", "tests/Medzo.Auth.IntegrationTests/"]
COPY ["tools/Medzo.Auth.AdminProvisioner/Medzo.Auth.AdminProvisioner.csproj", "tools/Medzo.Auth.AdminProvisioner/"]

# Restore dependencies
RUN dotnet restore

# Copy all the remaining source code
COPY . .

# Build the application
WORKDIR "/src/src/Medzo.Auth.Api"
RUN dotnet build "Medzo.Auth.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Medzo.Auth.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Medzo.Auth.Api.dll"]
