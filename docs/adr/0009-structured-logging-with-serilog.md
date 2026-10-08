# ADR 0009: Structured logging with Serilog · Logs estructurados con Serilog

- **Date · Fecha:** 2026-10-07
- **Status · Estado:** Accepted

## English

### Context
The PRD asks for basic observability: structured logs, one line per request with a correlation id, a health check that includes the database, and no secrets or personal data in the logs (#22). The API runs in a container, so logs go to the console and are collected from there.

### Options considered
1. **Built-in `Microsoft.Extensions.Logging` with the JSON console formatter**: no new package, but per-request logging (`UseHttpLogging`) logs headers and can log bodies and query strings unless carefully trimmed, and adding properties to every entry of a request needs scopes everywhere.
2. **Serilog (`Serilog.AspNetCore`, Apache 2.0)**: the de facto standard in .NET; `UseSerilogRequestLogging` writes exactly one line per request with the fields we choose, `LogContext` adds the correlation id to every entry, and the configuration lives in `appsettings`.
3. **OpenTelemetry logs and traces**: the long-term answer for distributed systems, but it needs a collector to be useful and the MVP is a single service.

### Decision
Use **Serilog** writing compact JSON to the console, configured from the `Serilog` section of `appsettings`. Each request writes one line with method, path (never the query string), status and duration. The correlation id is the W3C trace id of the request, returned as `X-Correlation-Id` and equal to the `traceId` of ProblemDetails. `/health` adds an EF Core check of PostgreSQL.

### Consequences
- A client reporting an error gives its `traceId`, and every log line of that request can be found by `CorrelationId`.
- Nothing that can hold secrets or personal data is logged by default: no bodies, headers or query strings; EF Core sensitive data logging stays off. Tests check that a login leaves no password, token or e-mail in the logs.
- Providers added to `ILoggingBuilder` still receive every event (`writeToProviders`), which the tests use to read the logs.
- The trace id is ready for OpenTelemetry if it is added later.

### Review when
The API is split into several services or deployed with a log or trace backend (then consider OpenTelemetry).

## Español

### Contexto
El PRD pide observabilidad básica: logs estructurados, una línea por petición con un correlation id, un health check que incluya la base de datos y nada de secretos ni datos personales en los logs (#22). La API corre en un contenedor, así que los logs van a la consola y se recogen desde ahí.

### Opciones consideradas
1. **`Microsoft.Extensions.Logging` integrado con el formato JSON de consola**: sin paquetes nuevos, pero el log de peticiones (`UseHttpLogging`) registra cabeceras y puede registrar cuerpos y query strings si no se recorta con cuidado, y añadir propiedades a cada entrada de una petición exige scopes por todas partes.
2. **Serilog (`Serilog.AspNetCore`, Apache 2.0)**: el estándar de facto en .NET; `UseSerilogRequestLogging` escribe exactamente una línea por petición con los campos que elegimos, `LogContext` añade el correlation id a cada entrada y la configuración vive en `appsettings`.
3. **Logs y trazas con OpenTelemetry**: la respuesta a largo plazo para sistemas distribuidos, pero necesita un colector para ser útil y el MVP es un solo servicio.

### Decisión
Usar **Serilog** escribiendo JSON compacto en la consola, configurado desde la sección `Serilog` de `appsettings`. Cada petición escribe una línea con método, ruta (nunca la query string), estado y duración. El correlation id es el trace id W3C de la petición, se devuelve como `X-Correlation-Id` y es igual al `traceId` de ProblemDetails. `/health` añade una comprobación de PostgreSQL con EF Core.

### Consecuencias
- Si un cliente reporta un error con su `traceId`, todas las líneas de log de esa petición se encuentran por `CorrelationId`.
- Por defecto no se registra nada que pueda contener secretos o datos personales: ni cuerpos, ni cabeceras, ni query strings; el sensitive data logging de EF Core sigue apagado. Las pruebas comprueban que un login no deja contraseña, token ni e-mail en los logs.
- Los proveedores añadidos a `ILoggingBuilder` siguen recibiendo cada evento (`writeToProviders`), algo que usan las pruebas para leer los logs.
- El trace id queda listo para OpenTelemetry si se añade más adelante.

### Revisar cuando
La API se divida en varios servicios o se despliegue con un backend de logs o trazas (entonces valorar OpenTelemetry).
