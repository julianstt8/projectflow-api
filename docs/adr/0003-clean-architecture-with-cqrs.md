# ADR 0003: Clean Architecture with CQRS · Clean Architecture con CQRS

- **Date · Fecha:** 2026-10-05
- **Status · Estado:** Accepted

## English

### Context
Business rules (roles, sprint states, task numbering, permissions) must be testable without a database or a web server and must not leak into controllers or EF Core.

### Options considered
1. Clean Architecture (Domain, Application, Infrastructure, Api) with CQRS use cases.
2. Classic layered architecture with services and repositories: simpler, but rules tend to spread across layers.
3. Single project with controllers calling EF Core directly: fastest at first, hardest to test and change.

### Decision
Clean Architecture. Dependencies point inward only (`Api -> Application -> Domain` and `Api -> Infrastructure -> Application`). Every use case is a command or a query with its own handler and validator. Handlers use ports from `Application/Abstractions` and never `ApplicationDbContext`.

### Consequences
- Domain and Application tests are fast and need no I/O.
- The rule is enforced by `tests/ProjectFlow.ArchitectureTests`, so a forbidden reference breaks the build instead of being caught in review.
- More files and indirection than a simple CRUD API; accepted on purpose.

### Review when
If the number of use cases or the team size makes the structure a burden, or the architecture tests start needing exceptions.

## Español

### Contexto
Las reglas de negocio (roles, estados de sprint, numeración de tareas, permisos) deben poder probarse sin base de datos ni servidor web, y no deben filtrarse a los controladores ni a EF Core.

### Opciones consideradas
1. Clean Architecture (Domain, Application, Infrastructure, Api) con casos de uso CQRS.
2. Arquitectura clásica por capas con servicios y repositorios: más simple, pero las reglas tienden a esparcirse.
3. Un solo proyecto con controladores que llaman a EF Core: lo más rápido al inicio, lo más difícil de probar y cambiar.

### Decisión
Clean Architecture. Las dependencias apuntan solo hacia adentro (`Api -> Application -> Domain` y `Api -> Infrastructure -> Application`). Cada caso de uso es un comando o una consulta con su handler y su validador. Los handlers usan puertos de `Application/Abstractions` y nunca `ApplicationDbContext`.

### Consecuencias
- Las pruebas de Domain y Application son rápidas y no necesitan entrada/salida.
- La regla la hace cumplir `tests/ProjectFlow.ArchitectureTests`: una referencia prohibida rompe la compilación en lugar de depender de la revisión.
- Más archivos e indirección que una API CRUD simple; se acepta a propósito.

### Revisar cuando
Si la cantidad de casos de uso o el tamaño del equipo vuelven la estructura una carga, o si las pruebas de arquitectura empiezan a necesitar excepciones.
