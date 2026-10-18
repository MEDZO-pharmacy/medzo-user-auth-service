# Auth: unit testing and mocking

## Additional business-rule tests

`RefreshTokenSecurityTests.cs` adds 13 cases for blank tokens, missing/expired/revoked sessions, inactive users, unauthorized roles, concurrent rotation failure, and repeated logout. Strict Moq dependencies verify that rejected sessions cannot issue tokens or rotate a session. Logout verifies the SHA-256 lookup and one revocation call.

## Run locally

Run from this repository:

```powershell
dotnet test tests/Medzo.Auth.UnitTests/Medzo.Auth.UnitTests.csproj --collect:"XPlat Code Coverage"
```

Latest local validation: **36 passing cases**, including the pre-existing tests. Each parameterized input counts as one case.

## Live demonstration

Push to `feature/se3112-unit-mocking`, open this repository's **Actions** tab, and open the newest unit-test workflow run. Expand the test step for console output, then show the **Summary** table for total/passed/failed/skipped counts.

The workflow uploads TRX test results and Coverlet Cobertura XML in its test-evidence artifact. Coverage measures executed lines/branches within the assemblies included by the collector; it is not the same as the percentage of passing tests. Compare reports only when their included assemblies and filters match.

