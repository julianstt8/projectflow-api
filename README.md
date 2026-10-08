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

The API listens on http://localhost:5080: open it in the browser for the **interactive API reference** (Scalar), in English or Spanish, where every endpoint explains what it does, who can call it, each error code it can return and example requests and responses, and can be tried (log in, then paste the access token as Bearer token). The OpenAPI documents are at `/openapi/en.json` and `/openapi/es.json`; all of them are available in Development only. Health check: `GET /health`. Local defaults can be overridden with a `.env` file based on `.env.example`.

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

### Projects

Projects live inside an organization (`/api/organizations/{organizationId}/projects`). Organization admins create them (and become their project manager) and can soft-delete them; project managers edit, archive (read-only) and manage project members, who must belong to the organization. Project keys like `WEB` are unique within the organization and stay reserved after deletion.

### Sprints

Sprints belong to a project (`.../projects/{projectId}/sprints`) and go **Planned → Active → Completed**. A project has **at most one active sprint** (RF-05), enforced by the domain and by a partial unique index, so two simultaneous starts can never both succeed. Completing a sprint moves its unfinished tasks back to the backlog; done tasks stay in the sprint.

**Epics** (`.../projects/{projectId}/epics`) group tasks and show their progress (tasks and done tasks). A closed epic accepts no new tasks until it is reopened.

**Tasks** (`.../projects/{projectId}/tasks`) get sequential keys per project (`WEB-12`), safe under concurrent creation: the project row is locked while numbering. They have a type, priority, story points (0–100), an assignee (someone who works in the project), a sprint (or the backlog) and an epic. Developers can change only tasks they reported or are assigned to; only project managers delete tasks (soft delete). Done tasks are read-only.

**Search** (RF-11): `GET .../tasks` filters by `status` (repeatable), `assigneeId` or `unassigned=true`, `sprintId` or `backlog=true`, `epicId`, `labelId` and `q`, which matches title words **ignoring case and accents** (`validacion` finds "Validación") or a key (`WEB-12`). Results are sorted (`sort=number|-number|updatedAt|-updatedAt`) and paged (`page`, `pageSize` up to 100, with `totalCount`).

Task status follows **ToDo → InProgress → Review → Done**, with Review → InProgress as the only step back (`POST .../tasks/{taskId}/status`, RF-07). Only project managers and organization admins reopen a done task (`POST .../tasks/{taskId}/reopen`, back to InProgress, RF-08).

**Comments** (`.../tasks/{taskId}/comments`, RF-09) accept any language and Unicode text (accents, ñ, emoji). Viewers read but cannot comment; only the author edits a comment; the author or a project manager deletes it. **Labels** (`.../projects/{projectId}/labels`) are managed by project managers (unique names per project, ignoring case) and added to tasks with `PUT/DELETE .../tasks/{taskId}/labels/{labelId}`.

### Activity log

Every relevant change (task created, status, assignee, sprint, epic, labels, comments, sprint and epic lifecycle, project archive and membership) is recorded with **who, what, when, old and new value** (RF-10) in the same transaction as the change: a rejected change leaves no trace. Project managers and organization admins read it, newest first and paged, at `.../projects/{projectId}/activity` (`?entityId=` for the history of one task). The log is **insert-only**: a database trigger rejects any update or delete.

### Logs and health

Logs are structured JSON on the console (Serilog): one line per request with method, path, status and duration, and a `CorrelationId` on every line. The correlation id is returned in the `X-Correlation-Id` header and equals the `traceId` of error responses, so an error a client reports can be found in the logs. Bodies, headers and query strings are never logged. `GET /health` answers `Healthy` (200) or `Unhealthy` (503) and checks that PostgreSQL is reachable. Log levels are set in the `Serilog` section of `appsettings.json`.

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

CI also checks for vulnerable packages and leaked secrets and reports test coverage. Architecture decisions are recorded in [`docs/adr`](docs/adr/README.md) and accepted shortcuts in [`docs/tech-debt.md`](docs/tech-debt.md).

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

La API queda en http://localhost:5080: ábrela en el navegador para ver la **referencia interactiva de la API** (Scalar), en inglés o en español, donde cada endpoint explica qué hace, quién puede llamarlo, cada código de error que puede devolver y ejemplos de petición y respuesta, y se puede probar (haz login y pega el access token como Bearer token). Los documentos OpenAPI están en `/openapi/en.json` y `/openapi/es.json`; todo solo en Development. Health check: `GET /health`. Los valores locales por defecto se pueden cambiar con un archivo `.env` basado en `.env.example`.

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

### Proyectos

Los proyectos viven dentro de una organización (`/api/organizations/{organizationId}/projects`). Los admins de la organización los crean (y quedan como project manager) y pueden eliminarlos (soft delete); los project managers los editan, los archivan (solo lectura) y gestionan sus miembros, que deben pertenecer a la organización. Las claves como `WEB` son únicas dentro de la organización y quedan reservadas tras eliminar el proyecto.

### Sprints

Los sprints pertenecen a un proyecto (`.../projects/{projectId}/sprints`) y pasan por **Planned → Active → Completed**. Un proyecto tiene **como máximo un sprint activo** (RF-05), garantizado por el dominio y por un índice único parcial, así que dos inicios simultáneos nunca pueden tener éxito a la vez. Al completar un sprint, sus tareas sin terminar vuelven al backlog; las terminadas se quedan en el sprint.

Las **épicas** (`.../projects/{projectId}/epics`) agrupan tareas y muestran su progreso (tareas y tareas terminadas). Una épica cerrada no acepta tareas nuevas hasta que se reabre.

Las **tareas** (`.../projects/{projectId}/tasks`) reciben claves consecutivas por proyecto (`WEB-12`), seguras ante creaciones simultáneas: la fila del proyecto se bloquea mientras se numera. Tienen tipo, prioridad, puntos (0–100), responsable (alguien que trabaja en el proyecto), sprint (o backlog) y épica. Un Developer solo puede cambiar las tareas que creó o tiene asignadas; solo los project managers las eliminan (soft delete). Las tareas terminadas son de solo lectura.

**Búsqueda** (RF-11): `GET .../tasks` filtra por `status` (repetible), `assigneeId` o `unassigned=true`, `sprintId` o `backlog=true`, `epicId`, `labelId` y `q`, que busca palabras del título **sin distinguir mayúsculas ni tildes** (`validacion` encuentra "Validación") o una clave (`WEB-12`). Los resultados se ordenan (`sort=number|-number|updatedAt|-updatedAt`) y se paginan (`page`, `pageSize` hasta 100, con `totalCount`).

El estado sigue **ToDo → InProgress → Review → Done**, con Review → InProgress como único retroceso (`POST .../tasks/{taskId}/status`, RF-07). Solo los project managers y admins de la organización reabren una tarea terminada (`POST .../tasks/{taskId}/reopen`, vuelve a InProgress, RF-08).

Los **comentarios** (`.../tasks/{taskId}/comments`, RF-09) admiten cualquier idioma y texto Unicode (tildes, ñ, emojis). Los Viewers los leen pero no comentan; solo el autor edita su comentario; lo elimina el autor o un project manager. Las **etiquetas** (`.../projects/{projectId}/labels`) las gestionan los project managers (nombres únicos por proyecto, sin distinguir mayúsculas) y se añaden a las tareas con `PUT/DELETE .../tasks/{taskId}/labels/{labelId}`.

### Registro de actividad

Cada cambio relevante (tarea creada, estado, responsable, sprint, épica, etiquetas, comentarios, ciclo de vida de sprints y épicas, archivado y miembros del proyecto) queda registrado con **quién, qué, cuándo, valor anterior y nuevo** (RF-10) en la misma transacción que el cambio: un cambio rechazado no deja rastro. Lo leen los project managers y admins de la organización, del más reciente al más antiguo y paginado, en `.../projects/{projectId}/activity` (`?entityId=` para el historial de una tarea). El registro es **solo de inserción**: un trigger de la base de datos rechaza cualquier modificación o borrado.

### Logs y salud

Los logs son JSON estructurado en la consola (Serilog): una línea por petición con método, ruta, estado y duración, y un `CorrelationId` en cada línea. El correlation id se devuelve en la cabecera `X-Correlation-Id` y es igual al `traceId` de las respuestas de error, así que un error que reporta un cliente se encuentra en los logs. Nunca se registran cuerpos, cabeceras ni query strings. `GET /health` responde `Healthy` (200) o `Unhealthy` (503) y comprueba que PostgreSQL responde. Los niveles de log se configuran en la sección `Serilog` de `appsettings.json`.

### Roles y permisos

La matriz completa está en la sección en inglés. Los roles se comprueban contra la base de datos en cada petición (nunca van en el token). Los admins de la organización tienen acceso total a todos sus proyectos; el resto de miembros solo ve los proyectos donde tiene un rol (Project Manager, Developer o Viewer). Un Developer solo puede editar las tareas que creó o que tiene asignadas.

### Errores

Todos los errores se devuelven como ProblemDetails (RFC 9457, `application/problem+json`) con un `code` estable y un `traceId`: 400 petición inválida (con `errors` por campo), 401/403 sin autenticar / sin permiso, 404 no existe o no es visible para el usuario, 409 choca con datos existentes (valor duplicado, cambio concurrente), 422 petición válida que rompe una regla de negocio.

El CI (GitHub Actions) revisa el formato, compila, ejecuta las pruebas y construye la imagen Docker en cada pull request.

El CI también revisa paquetes con vulnerabilidades y secretos filtrados, y reporta la cobertura de pruebas. Las decisiones de arquitectura están en [`docs/adr`](docs/adr/README.md) y los atajos aceptados en [`docs/tech-debt.md`](docs/tech-debt.md).
