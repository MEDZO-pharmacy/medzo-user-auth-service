# Medzo User Auth Service

A .NET 10 authentication and authorization microservice for the Medzo Pharmacy platform, built with Clean Architecture principles.

## Architecture

| Layer | Project | Responsibility |
|-------|---------|----------------|
| **API** | `Medzo.Auth.Api` | Controllers, middleware, DI configuration |
| **Application** | `Medzo.Auth.Application` | DTOs, interfaces, services, validators |
| **Domain** | `Medzo.Auth.Domain` | Entities, enums, domain exceptions |
| **Infrastructure** | `Medzo.Auth.Infrastructure` | EF Core, repositories, JWT, password hashing |

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQL Server (local or Docker)

### Run Locally

Create the local environment file and replace every `CHANGE_ME` value before
starting the application. The `.env` file is ignored by Git.

```powershell
Copy-Item .env.example .env

# Restore dependencies
dotnet restore

# Run the API
dotnet run --project src/Medzo.Auth.Api

# Run tests
dotnet test
```

### Docker

Docker configuration is also read from `.env` at container startup. Secrets are
not copied into the image because `.dockerignore` excludes all `.env` files.

```powershell
Copy-Item .env.example .env
# Edit .env and replace every CHANGE_ME value before continuing.

docker build -f docker/Dockerfile -t medzo-auth-service .
docker run --rm --name medzo-auth-service --env-file .env -p 8080:8080 medzo-auth-service
```

Alternatively, Compose builds the image and supplies the same runtime environment:

```powershell
docker compose up --build
```

The API is then available over HTTP at `http://localhost:8080`. Environment
variables use ASP.NET Core's double-underscore notation: `Jwt__Secret` maps to
`Jwt:Secret`, and `ConnectionStrings__DefaultConnection` maps to the default
database connection string.

## API Endpoints

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/api/auth/login` | User login | No |
| POST | `/api/auth/register` | User registration | No |
| POST | `/api/auth/refresh` | Refresh access token | No |
| POST | `/api/auth/revoke` | Revoke refresh token | No |
| GET | `/api/users` | Get all users | Admin |
| POST | `/api/users` | Create a user and assign one role | Admin |
| GET | `/api/users/{id}` | Get user by ID | Self or Admin |
| PUT | `/api/users/{id}` | Update user | Self or Admin |
| DELETE | `/api/users/{id}` | Delete user | Admin |

When `POST /api/users` finds another account with the same first and last name, it returns
`409 Conflict` with `code: "potential_duplicate"`. After reviewing the matches, an admin can
resubmit the same request with `confirmPotentialDuplicate: true`. Usernames and email addresses
remain strictly unique and cannot be overridden.

## License

Proprietary — Medzo Pharmacy © 2026
