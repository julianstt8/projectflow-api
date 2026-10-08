# ADR 0007: API reference with built-in OpenAPI and Scalar · Referencia de la API con OpenAPI integrado y Scalar

- **Date · Fecha:** 2026-10-07
- **Status · Estado:** Accepted

## English

### Context
The PRD asks for every endpoint to be documented "with Swagger" and for the API to be easy to try. .NET 10 generates the OpenAPI document itself (`Microsoft.AspNetCore.OpenApi`) and no longer ships Swashbuckle in its templates.

### Options considered
1. **Swashbuckle (Swagger UI + generator)**: well known, but it duplicates the document generator that .NET 10 already has and its maintenance has been irregular.
2. **Built-in OpenAPI + Swagger UI only**: keeps the official generator, but Swagger UI is dated and weaker at trying authenticated endpoints.
3. **Built-in OpenAPI + Scalar (MIT)**: official generator, modern reference with request examples and Bearer authentication, a single package.

### Decision
Use the built-in OpenAPI document with **Scalar** as the interactive reference, **in Development only**. Summaries and field descriptions come from XML comments; the Api project does not suppress CS1591.

### Consequences
- "Swagger" in the PRD is met by its intent (OpenAPI 3.1 document + interactive reference), not by the Swagger UI product.
- Every public type and action of the Api project needs an XML comment; `ApiReferenceTests` fails if an operation has no summary.
- In other environments the document is not exposed; a public reference would need an explicit decision (and probably authentication).

### Review when
The API is deployed for a public demo, or Scalar stops being maintained.

## Español

### Contexto
El PRD pide que cada endpoint esté documentado "con Swagger" y que la API sea fácil de probar. .NET 10 genera el documento OpenAPI por sí mismo (`Microsoft.AspNetCore.OpenApi`) y ya no incluye Swashbuckle en sus plantillas.

### Opciones consideradas
1. **Swashbuckle (Swagger UI + generador)**: conocido, pero duplica el generador que .NET 10 ya trae y su mantenimiento ha sido irregular.
2. **OpenAPI integrado + solo Swagger UI**: mantiene el generador oficial, pero Swagger UI está anticuado y es más débil para probar endpoints autenticados.
3. **OpenAPI integrado + Scalar (MIT)**: generador oficial, referencia moderna con ejemplos y autenticación Bearer, un solo paquete.

### Decisión
Usar el documento OpenAPI integrado con **Scalar** como referencia interactiva, **solo en Development**. Los resúmenes y descripciones de campos salen de los comentarios XML; el proyecto Api no suprime CS1591.

### Consecuencias
- El "Swagger" del PRD se cumple por su intención (documento OpenAPI 3.1 + referencia interactiva), no por el producto Swagger UI.
- Cada tipo público y acción del proyecto Api necesita un comentario XML; `ApiReferenceTests` falla si una operación no tiene resumen.
- En otros entornos el documento no se expone; una referencia pública requeriría una decisión explícita (y probablemente autenticación).

### Revisar cuando
La API se despliegue para una demo pública, o Scalar deje de mantenerse.
