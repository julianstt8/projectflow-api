# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

ProjectFlow API: an enterprise REST API for software project management (mini-Jira).
Organizations, projects, sprints, epics and tasks, with JWT + refresh tokens, RBAC and an audit log.
The PRD and data model live in Notion ("PRD – Proyecto 1" and "Modelo de Datos – Proyecto 1").

## Stack

- .NET 10 (LTS), ASP.NET Core with **controllers**
- CQRS with [`Mediator`](https://github.com/martinothamar/Mediator) (MIT, source generator). **Do not use MediatR** (dual RPL/commercial license since v13).
- FluentValidation, run through `ValidationBehavior` in the Mediator pipeline
- PostgreSQL + EF Core (milestone 2), xUnit, Testcontainers for integration tests

## Architecture

Clean Architecture. Dependencies point inward only:

```
Api -> Application -> Domain
Api -> Infrastructure -> Application
```

- `Domain`: entities, value objects, domain rules and events. No references to other layers or frameworks.
- `Application`: commands, queries, handlers, validators, interfaces (ports).
- `Infrastructure`: EF Core, PostgreSQL, JWT, external services.
- `Api`: controllers, auth, OpenAPI, ProblemDetails.

`tests/ProjectFlow.ArchitectureTests` enforces these rules; keep it green.
New Mediator handlers in Application are picked up because `Program.cs` sets `options.Assemblies`.

## Commands

```bash
dotnet build
dotnet test
dotnet run --project src/ProjectFlow.Api
dotnet format                 # CI runs `dotnet format --verify-no-changes`
docker compose up --build     # API on http://localhost:5080 + PostgreSQL
```

### Database (EF Core)

```bash
dotnet tool restore           # installs the local dotnet-ef tool
dotnet ef migrations add <Name> --project src/ProjectFlow.Infrastructure --startup-project src/ProjectFlow.Api --output-dir Persistence/Migrations
```

- Tables and columns are snake_case (`EFCore.NamingConventions`); enums are stored as text, so `ORDER BY` an enum in SQL is alphabetical: order by enum value in memory after the query.
- In read queries, apply `Where`/`OrderBy` on the entity **before** projecting to a response record: EF cannot translate members of a record built with its constructor.
- Integration tests set up data through `api.CreateOrganizationAsync()` → `TestOrganization` (members, projects, project roles) and `api.AddTaskAsync(...)` for tasks.
- Entities are materialized through their constructors: constructor parameter names must match property names.
- Every model change needs a migration; `ProjectFlow.Infrastructure.Tests` fails if one is missing.
- Migrations run on startup only in Development with `Database:MigrateOnStartup=true` (set by docker-compose).
- Demo data (`Persistence/Seeding/DevelopmentDataSeeder.cs`) is loaded only in Development with `Database:SeedOnStartup=true`, once (idempotent). Build it through domain methods, never with raw inserts.
- Running the API outside Docker needs `ConnectionStrings:Default`, e.g. `dotnet user-secrets set ConnectionStrings:Default "<connection string>" --project src/ProjectFlow.Api`.

## API conventions

- **Secure by default**: a fallback authorization policy requires an authenticated user on every endpoint. Mark public endpoints with `[AllowAnonymous]` explicitly.
- Access tokens carry only `sub`, `email`, `name` and `jti`. **Never put roles in the token**: they are resolved per request from the database (PRD 4.1).
- Controllers inherit `ApiControllerBase`, send a command/query through `ISender` and return `ErrorResult(result.Error)` on failure (ProblemDetails with a `code` extension). Validation failures from the Mediator pipeline become 400 via `ValidationExceptionHandler`.
- **Errors**: expected failures are `Result`s with an `Error` (never exceptions). Pick the `ErrorType` carefully, it decides the status (`ProblemDetailsSetup.ToStatusCode`, the only mapping): `Validation` 400, `Unauthorized` 401, `Forbidden` 403, `NotFound` 404, `Conflict` 409 (**collides with existing data**: duplicates, concurrency), `BusinessRule` 422 (**well-formed request that breaks a rule**: invalid transition, last admin, archived project). Every error response is `application/problem+json` with a stable `code` and a `traceId`; framework errors get a default code. `IUnitOfWork` turns unique-index violations into `UniqueConstraintViolationException` and concurrency errors into `ConcurrencyConflictException` (both 409 via `PersistenceExceptionHandler`).
- Mediator handlers must be `public` (the source generator runs in the Api project).
- Organization-scoped endpoints live under `/api/organizations/{organizationId}/...` (the route value **must** be named `organizationId`) and carry `[Authorize(Policy = OrganizationPolicies.Member)]` or `.Admin`. The policy checks membership in the database on every request; non-members get **404** (the organization's existence is not revealed), members without the role get 403. Never expose organization-owned data without one of these policies.
- Project-scoped endpoints live under `/api/organizations/{organizationId}/projects/{projectId}/...` and carry `[RequireProjectPermission(ProjectPermission.X)]`. The matrix lives in `Domain/Projects/ProjectPermissions.cs` (organization admins have every permission). Users who cannot see the project get 404, users missing the permission get 403. Rules on a specific resource (developers edit only tasks they reported or are assigned to) are checked in the use case with `IProjectAccessResolver` → `ProjectAccess.CanEditTask`.
- Changing the permission matrix means updating `ProjectPermissionsTests` (domain) and `ProjectAuthorizationTests` (API) on purpose: both spell the PRD matrix out by hand.
- Enums are serialized as strings in JSON.
- Refresh tokens: one family per login, rotated on every use, family revoked on reuse; stored only as SHA-256. The refresh use case handles `ConcurrencyConflictException` itself (a lost race counts as reuse).
- Task numbering (RF-04) locks the project row (`IProjectRepository.LockForTaskNumberingAsync` inside `IUnitOfWork.BeginTransactionAsync`) so concurrent creations wait instead of colliding. Task changes go through `TaskEditor`, which applies the developer "own or assigned" rule; use it for every new task-changing use case.
- **Activity log (RF-10)**: entities raise `ProjectActivityEvent`s (`Domain/Activity/ActivityEvents.cs`) only when a value really changes; `UnitOfWork` turns them into `ActivityLog` rows (actor = current user, time = save time) in the same transaction. New project-scoped changes must raise an event. `activity_logs` is insert-only (DB trigger): never update or delete it.
- Handlers use ports from `Application/Abstractions` (`IUserRepository`, `IUnitOfWork`, `IPasswordHasher`, `IAccessTokenGenerator`, `TimeProvider`); never `ApplicationDbContext` directly.

## Tests

- `ProjectFlow.Domain.Tests` / `ProjectFlow.Application.Tests`: fast unit tests, no I/O.
- `ProjectFlow.Infrastructure.Tests`: EF Core model checks (no database) plus query filters and seeding against PostgreSQL.
- `ProjectFlow.Api.IntegrationTests`: the real API (`ProjectFlowApiFactory`) against PostgreSQL; covers migrations, constraints and concurrency, and later the HTTP endpoints.
- PostgreSQL tests use **Testcontainers** (throwaway `postgres:17-alpine` per test run), so **Docker must be running** for `dotnet test`. Never use the EF in-memory provider.
- Tests that need a database share one container per assembly through an xUnit collection fixture; use unique values (`TestData.Unique()`) instead of cleaning tables.

## Rules

- Code, commits, issues and PRs in **English**. Public docs (README, docs/) bilingual EN/ES.
- Conventional Commits (`feat: add task endpoints`). Branches like `feature/task-status-flow`.
- Nothing reaches `main` without a Pull Request and passing tests (from milestone 1 onwards).
- Secrets never in the repository: use user-secrets or environment variables.
- Warnings are errors (`Directory.Build.props`); fix them, do not suppress them.
- Keep the MVP scope of the PRD; out-of-scope ideas go to Notion, not to code.
