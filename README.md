# ProjectFlow API

**English** | [Español](#español)

Enterprise REST API for software project management (mini-Jira): organizations, projects, sprints, epics and tasks, with JWT authentication, role-based access control and an audit log.

> 🚧 Work in progress: base solution (milestone 1).

## Stack

.NET 10 · ASP.NET Core · Clean Architecture · CQRS (Mediator) · FluentValidation · EF Core + PostgreSQL · xUnit

## Architecture

```
src/
  ProjectFlow.Domain          Entities and business rules
  ProjectFlow.Application     Use cases (commands/queries), validation
  ProjectFlow.Infrastructure  Persistence, authentication, external services
  ProjectFlow.Api             HTTP API (controllers, OpenAPI)
tests/
  ProjectFlow.Domain.Tests
  ProjectFlow.Application.Tests
  ProjectFlow.Api.IntegrationTests
  ProjectFlow.ArchitectureTests   Enforces layer dependencies
```

## Getting started

With Docker (API + PostgreSQL, no other setup needed):

```bash
docker compose up --build
```

The API listens on http://localhost:5080 (health check: `GET /health`). Local defaults can be overridden with a `.env` file based on `.env.example`.

With the [.NET 10 SDK](https://dotnet.microsoft.com/download):

```bash
dotnet build
dotnet test
dotnet run --project src/ProjectFlow.Api
```

Docker Compose applies the database migrations on startup. To run the API outside Docker, start only the database (`docker compose up db`) and set the connection string with user-secrets:

```bash
dotnet user-secrets set ConnectionStrings:Default "Host=localhost;Port=5432;Database=projectflow;Username=projectflow;Password=projectflow_dev" --project src/ProjectFlow.Api
```

CI (GitHub Actions) checks formatting, builds, runs the tests and builds the Docker image on every pull request.

---

## Español

API REST empresarial para gestionar proyectos de software (estilo mini-Jira): organizaciones, proyectos, sprints, épicas y tareas, con autenticación JWT, control de acceso por roles y registro de auditoría.

> 🚧 En construcción: solución base (hito 1).

### Stack

.NET 10 · ASP.NET Core · Clean Architecture · CQRS (Mediator) · FluentValidation · EF Core + PostgreSQL · xUnit

### Arquitectura

Cuatro capas con dependencias hacia adentro: `Api` → `Application` → `Domain`, e `Infrastructure` implementa los puertos de `Application`. El proyecto `ProjectFlow.ArchitectureTests` verifica estas reglas en cada ejecución de pruebas.

### Cómo ejecutarlo

Con Docker (API + PostgreSQL, sin más configuración):

```bash
docker compose up --build
```

La API queda en http://localhost:5080 (health check: `GET /health`). Los valores locales por defecto se pueden cambiar con un archivo `.env` basado en `.env.example`.

Con el [SDK de .NET 10](https://dotnet.microsoft.com/download):

```bash
dotnet build
dotnet test
dotnet run --project src/ProjectFlow.Api
```

Docker Compose aplica las migraciones de la base de datos al arrancar. Para ejecutar la API fuera de Docker, levanta solo la base de datos (`docker compose up db`) y configura la cadena de conexión con user-secrets:

```bash
dotnet user-secrets set ConnectionStrings:Default "Host=localhost;Port=5432;Database=projectflow;Username=projectflow;Password=projectflow_dev" --project src/ProjectFlow.Api
```

El CI (GitHub Actions) revisa el formato, compila, ejecuta las pruebas y construye la imagen Docker en cada pull request.
