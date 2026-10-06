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

### Demo data

In Development, Docker Compose also loads fictional demo data (once): two organizations (**Acme Software** and **Globex Corporation**), three projects with sprints, epics, labels, tasks in every status and comments. Every demo user signs in with the password `ProjectFlow-Dev-2026` (local development only):

| User | Role |
|---|---|
| ana.admin@example.com | Acme admin |
| bruno.pm@example.com | Project manager (WEB, MOB) |
| carla.dev@example.com | Developer at Acme, viewer at Globex |
| diego.dev@example.com | Developer (WEB) |
| elena.viewer@example.com | Viewer (WEB) |
| frank.admin@example.com | Globex admin, project manager (DATA) |
| grace.dev@example.com | Developer (DATA) |

### Authentication

Every endpoint requires a JWT access token except registration, login and the health check:

```bash
curl -X POST http://localhost:5080/api/auth/login -H "Content-Type: application/json"   -d '{"email":"ana.admin@example.com","password":"ProjectFlow-Dev-2026"}'
curl http://localhost:5080/api/auth/me -H "Authorization: Bearer <accessToken>"
```

Access tokens last 15 minutes. Login also returns a **refresh token** (7 days): `POST /api/auth/refresh` exchanges it for a new pair and it works only once (rotation); replaying an already used refresh token revokes the whole session. `POST /api/auth/logout` ends the session. Only a SHA-256 hash of refresh tokens is stored. The signing key comes from configuration (`Jwt:SigningKey`, at least 32 characters) via user-secrets or environment variables and is never committed. In Development, if no key is set, a random one is generated on startup (tokens stop working after a restart); in any other environment the API does not start without a valid key.

### Organizations

A user can belong to several organizations as `Admin` or `Member` (`/api/organizations`): whoever creates an organization becomes its admin; admins add members by e-mail, change roles and remove members; any member can leave. The last admin can never be removed or demoted. Users who are not members get `404` for an organization, so they cannot even tell it exists.

### Roles and permissions

| Action | Org admin | Project manager | Developer | Viewer |
|---|---|---|---|---|
| View project and tasks | ✅ | ✅ | ✅ | ✅ |
| Create and edit projects, manage project members | ✅ | ✅ | | |
| Manage sprints and epics | ✅ | ✅ | | |
| Create tasks, comment | ✅ | ✅ | ✅ | |
| Edit and move tasks | ✅ any | ✅ any | own or assigned | |
| Reopen done tasks, view activity log | ✅ | ✅ | | |

Roles are checked against the database on every request (never stored in the token). Organization admins have full access to every project of their organization; other members only see projects where they have a role.

### Errors

Every error is returned as [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) ProblemDetails (`application/problem+json`) with a stable `code` and a `traceId`:

```json
{ "status": 422, "title": "The last admin of an organization cannot be removed or demoted.", "code": "Organization.LastAdmin", "traceId": "00-…" }
```

| Status | Meaning |
|---|---|
| 400 | Invalid request; `errors` lists the problems per field |
| 401 / 403 | Not authenticated / not allowed |
| 404 | Not found, or not visible to the current user |
| 409 | Conflicts with existing data (duplicate value, concurrent change) |
| 422 | Valid request that breaks a business rule |

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

### Datos de ejemplo

En Development, Docker Compose también carga datos de ejemplo ficticios (una sola vez): dos organizaciones (**Acme Software** y **Globex Corporation**), tres proyectos con sprints, épicas, etiquetas, tareas en todos los estados y comentarios. Todos los usuarios de ejemplo entran con la contraseña `ProjectFlow-Dev-2026` (solo para desarrollo local); la tabla de usuarios y roles está en la sección en inglés.

### Autenticación

Todos los endpoints exigen un token JWT, excepto el registro, el login y el health check. Ejemplo con `curl` en la sección en inglés.

Los tokens de acceso duran 15 minutos. El login también devuelve un **refresh token** (7 días): `POST /api/auth/refresh` lo cambia por un par nuevo y solo sirve una vez (rotación); reutilizar un refresh token ya usado revoca toda la sesión. `POST /api/auth/logout` cierra la sesión. De los refresh tokens solo se guarda su hash SHA-256. La clave de firma viene de la configuración (`Jwt:SigningKey`, mínimo 32 caracteres) mediante user-secrets o variables de entorno y nunca se sube al repositorio. En Development, si no hay clave, se genera una aleatoria al arrancar (los tokens dejan de valer al reiniciar); en cualquier otro entorno la API no arranca sin una clave válida.

### Organizaciones

Un usuario puede pertenecer a varias organizaciones como `Admin` o `Member` (`/api/organizations`): quien crea una organización es su admin; los admins añaden miembros por e-mail, cambian roles y quitan miembros; cualquier miembro puede salir. El último admin nunca puede ser eliminado ni degradado. Quien no es miembro recibe `404`, así que ni siquiera sabe si la organización existe.

### Roles y permisos

La matriz completa está en la sección en inglés. Los roles se comprueban contra la base de datos en cada petición (nunca van en el token). Los admins de la organización tienen acceso total a todos sus proyectos; el resto de miembros solo ve los proyectos donde tiene un rol (Project Manager, Developer o Viewer). Un Developer solo puede editar las tareas que creó o que tiene asignadas.

### Errores

Todos los errores se devuelven como ProblemDetails (RFC 9457, `application/problem+json`) con un `code` estable y un `traceId`: 400 petición inválida (con `errors` por campo), 401/403 sin autenticar / sin permiso, 404 no existe o no es visible para el usuario, 409 choca con datos existentes (valor duplicado, cambio concurrente), 422 petición válida que rompe una regla de negocio.

El CI (GitHub Actions) revisa el formato, compila, ejecuta las pruebas y construye la imagen Docker en cada pull request.
