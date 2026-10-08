# Architecture Decision Records (ADR) · Registro de decisiones

**English** | [Español](#español)

Short notes that explain **why** an important technical decision was taken, so it is not undone by accident later. One file per decision, numbered, never deleted: when a decision changes, the old record is marked `Superseded` and a new one is written.

| # | Decision | Status |
|---|---|---|
| [0001](0001-dotnet-10-and-postgresql.md) | .NET 10 (LTS) and PostgreSQL | Accepted |
| [0002](0002-mediator-instead-of-mediatr.md) | Mediator (MIT) instead of MediatR | Accepted |
| [0003](0003-clean-architecture-with-cqrs.md) | Clean Architecture with CQRS | Accepted |
| [0004](0004-roles-resolved-per-request.md) | Roles resolved per request, never in the token | Accepted |
| [0005](0005-refresh-token-rotation.md) | Refresh token rotation with family revocation | Accepted |
| [0006](0006-insert-only-activity-log.md) | Insert-only activity log enforced by a database trigger | Accepted |
| [0007](0007-api-reference-with-openapi-and-scalar.md) | API reference with built-in OpenAPI and Scalar (Development only) | Accepted; texts replaced by 0008 |
| [0008](0008-bilingual-api-reference-catalog.md) | Bilingual API reference (EN/ES) from a text catalog | Accepted |
| [0009](0009-structured-logging-with-serilog.md) | Structured logging with Serilog, request logging and correlation id | Accepted |

To add one, copy [`template.md`](template.md), take the next number and add it to this table. Review the records when the "Review when" date or condition arrives.

---

## Español

Notas cortas que explican **por qué** se tomó una decisión técnica importante, para que nadie la deshaga por accidente más adelante. Un archivo por decisión, numerado y sin borrarse: cuando una decisión cambia, el registro anterior se marca como `Superseded` (reemplazada) y se escribe uno nuevo.

Para agregar una, copia [`template.md`](template.md), toma el siguiente número y añádela a la tabla. Revisa los registros cuando llegue la fecha o la condición de "Review when" (revisar cuando).
