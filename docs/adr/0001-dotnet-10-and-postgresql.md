# ADR 0001: .NET 10 (LTS) and PostgreSQL · .NET 10 (LTS) y PostgreSQL

- **Date · Fecha:** 2026-10-05
- **Status · Estado:** Accepted

## English

### Context
The project is a business-style REST API (organizations, projects, tasks, RBAC, audit log) that must be maintainable for years and show enterprise practices.

### Options considered
1. .NET 10 (LTS) with PostgreSQL: long-term support, strong typing, first-class ASP.NET Core and EF Core, open-source database.
2. Another stack (Node.js, Java): viable, but less aligned with the goal of the project.
3. SQL Server: capable, but adds licensing and a heavier local setup.

### Decision
Use .NET 10 (the current long-term-support release, pinned in `global.json`) and PostgreSQL 17, accessed with EF Core and Npgsql.

### Consequences
- Security and bug-fix updates are guaranteed for the LTS period; the SDK version is pinned so CI and local builds agree.
- PostgreSQL features (partial unique indexes, triggers, `unaccent`) are used on purpose and are part of the design.
- Integration tests run against a real PostgreSQL through Testcontainers, so Docker is required to run the tests.

### Review when
Before the end of the .NET 10 support period, or if the hosting target cannot run PostgreSQL.

## Español

### Contexto
El proyecto es una API REST de tipo empresarial (organizaciones, proyectos, tareas, RBAC, registro de actividad) que debe poder mantenerse por años y mostrar prácticas profesionales.

### Opciones consideradas
1. .NET 10 (LTS) con PostgreSQL: soporte a largo plazo, tipado fuerte, ASP.NET Core y EF Core de primer nivel, base de datos abierta.
2. Otro stack (Node.js, Java): posible, pero menos alineado con el objetivo del proyecto.
3. SQL Server: capaz, pero suma licencias y una instalación local más pesada.

### Decisión
Usar .NET 10 (la versión de soporte a largo plazo actual, fijada en `global.json`) y PostgreSQL 17, con EF Core y Npgsql.

### Consecuencias
- Las actualizaciones de seguridad y errores están garantizadas durante el periodo LTS; la versión del SDK está fijada para que el CI y las compilaciones locales coincidan.
- Se usan a propósito funciones de PostgreSQL (índices únicos parciales, triggers, `unaccent`) y forman parte del diseño.
- Las pruebas de integración corren contra un PostgreSQL real con Testcontainers, así que se necesita Docker para ejecutar las pruebas.

### Revisar cuando
Antes de que termine el soporte de .NET 10, o si el destino de despliegue no puede ejecutar PostgreSQL.
