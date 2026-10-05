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

## Rules

- Code, commits, issues and PRs in **English**. Public docs (README, docs/) bilingual EN/ES.
- Conventional Commits (`feat: add task endpoints`). Branches like `feature/task-status-flow`.
- Nothing reaches `main` without a Pull Request and passing tests (from milestone 1 onwards).
- Secrets never in the repository: use user-secrets or environment variables.
- Warnings are errors (`Directory.Build.props`); fix them, do not suppress them.
- Keep the MVP scope of the PRD; out-of-scope ideas go to Notion, not to code.
