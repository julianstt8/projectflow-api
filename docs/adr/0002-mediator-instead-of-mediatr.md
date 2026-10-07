# ADR 0002: Mediator (MIT) instead of MediatR · Mediator (MIT) en lugar de MediatR

- **Date · Fecha:** 2026-10-05
- **Status · Estado:** Accepted

## English

### Context
The application layer uses CQRS: commands and queries go through a mediator with a pipeline (validation). The best-known library, MediatR, moved to a dual license (RPL/commercial) starting with version 13.

### Options considered
1. MediatR: very common, but the license change adds legal risk for a project that may become public or commercial.
2. [Mediator](https://github.com/martinothamar/Mediator) by Martin Othamar: MIT license, source generator (no reflection at runtime), similar API.
3. Own implementation: no dependency, but more code to maintain.

### Decision
Use the `Mediator` library (MIT). Do not use MediatR.

### Consequences
- No license keys or commercial obligations.
- Handlers must be `public`, because the source generator runs in the Api project, and `Program.cs` sets `options.Assemblies` so Application handlers are discovered.
- Cross-cutting behavior such as validation lives in pipeline behaviors (`ValidationBehavior`).

### Review when
If the library is abandoned, or MediatR returns to a permissive license and there is a reason to switch.

## Español

### Contexto
La capa de aplicación usa CQRS: los comandos y consultas pasan por un mediador con un pipeline (validación). La librería más conocida, MediatR, pasó a una licencia dual (RPL/comercial) desde la versión 13.

### Opciones consideradas
1. MediatR: muy común, pero el cambio de licencia añade riesgo legal para un proyecto que puede volverse público o comercial.
2. [Mediator](https://github.com/martinothamar/Mediator) de Martin Othamar: licencia MIT, generador de código (sin reflexión en ejecución), API parecida.
3. Implementación propia: sin dependencia, pero más código que mantener.

### Decisión
Usar la librería `Mediator` (MIT). No usar MediatR.

### Consecuencias
- Sin claves de licencia ni obligaciones comerciales.
- Los handlers deben ser `public`, porque el generador de código corre en el proyecto Api, y `Program.cs` define `options.Assemblies` para descubrir los handlers de Application.
- Lo transversal, como la validación, vive en comportamientos del pipeline (`ValidationBehavior`).

### Revisar cuando
Si la librería se abandona, o si MediatR vuelve a una licencia permisiva y hay una razón para cambiar.
