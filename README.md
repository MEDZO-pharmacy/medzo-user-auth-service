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

## Mutation Testing

**Owner:** IT24101508

**Tool:** Stryker.NET 5 with xUnit, Moq, and FluentAssertions

Mutation testing deliberately changes production logic and reruns the relevant tests. A killed
mutant proves that a test detected the injected fault; a survivor identifies an assertion gap,
an untested branch, or a behavior that cannot be distinguished reliably by the current design.

The configuration at `tests/Medzo.Auth.UnitTests/stryker-config.json` targets two
security-critical application classes:

- `Services/AuthService.cs` for login, registration, refresh-token rotation, revocation, claims,
  and role-based website access;
- `Services/StaffRoleResolver.cs` for staff-ID normalization, validation, role mapping, and signup
  eligibility.

Install the pinned repository-local tool and run the quality gate:

```powershell
dotnet tool restore
dotnet test tests/Medzo.Auth.UnitTests/Medzo.Auth.UnitTests.csproj --configuration Release
Push-Location tests/Medzo.Auth.UnitTests
dotnet stryker --skip-version-check
Pop-Location
```

Stryker writes timestamped HTML, JSON, and Markdown reports under
`tests/Medzo.Auth.UnitTests/StrykerOutput/<timestamp>/reports/`. The directory is ignored by Git.

### Verified results

| Metric | Baseline | Strengthened suite |
| --- | ---: | ---: |
| Unit tests | 23 | 73 |
| Scored mutants | 97 | 97 |
| Killed | 52 | 96 |
| Survived | 23 | 1 |
| No coverage | 22 | 0 |
| Timed out | 0 | 0 |
| Mutation score | 53.61% | **98.97%** |

The single survivor changes refresh-token expiry from `ExpiresAt <= now` to `ExpiresAt < now`.
Distinguishing those expressions requires the token expiry to equal the private `DateTime.UtcNow`
value captured inside the method at the exact tick. Without changing production code to inject a
clock, such a test would be timing-dependent and unreliable. The boundary is documented rather
than hidden or excluded.

### Automated quality gate

`.github/workflows/auth-mutation-testing.yml` runs on relevant branch pushes, pull requests into
`main`, and manual dispatch. It restores the pinned tools, runs all auth unit tests, executes
Stryker with an 80% break threshold, and uploads the generated reports for 14 days even if the
quality gate fails.

### Two-minute live demo

1. Run the unit-test command and show all 73 isolated tests passing.
2. Run `dotnet stryker --skip-version-check` from `tests/Medzo.Auth.UnitTests`.
3. Explain the final killed, survived, no-coverage, and mutation-score values.
4. Open the newest `mutation-report.html` and inspect the remaining time-boundary survivor.
5. Contrast the 53.61% baseline with 98.97% to show how mutation feedback improved the suite.

## License

Proprietary — Medzo Pharmacy © 2026
