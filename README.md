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

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build
dotnet test
dotnet run --project src/ProjectFlow.Api
```

Health check: `GET /health`.

---

## Español

API REST empresarial para gestionar proyectos de software (estilo mini-Jira): organizaciones, proyectos, sprints, épicas y tareas, con autenticación JWT, control de acceso por roles y registro de auditoría.

> 🚧 En construcción: solución base (hito 1).

### Stack

.NET 10 · ASP.NET Core · Clean Architecture · CQRS (Mediator) · FluentValidation · EF Core + PostgreSQL · xUnit

### Arquitectura

Cuatro capas con dependencias hacia adentro: `Api` → `Application` → `Domain`, e `Infrastructure` implementa los puertos de `Application`. El proyecto `ProjectFlow.ArchitectureTests` verifica estas reglas en cada ejecución de pruebas.

### Cómo ejecutarlo

Requisitos: [SDK de .NET 10](https://dotnet.microsoft.com/download).

```bash
dotnet build
dotnet test
dotnet run --project src/ProjectFlow.Api
```

Health check: `GET /health`.
