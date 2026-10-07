# ADR 0004: Roles resolved per request, never in the token · Roles resueltos en cada solicitud, nunca en el token

- **Date · Fecha:** 2026-10-05
- **Status · Estado:** Accepted

## English

### Context
Users have different roles per organization and per project, and those roles change. If roles were stored in the JWT, a removed or demoted user would keep their old access until the token expired.

### Options considered
1. Put roles in the access token: no database lookup per request, but stale permissions.
2. Resolve roles from the database on every request: one extra query, always current.

### Decision
Access tokens carry only `sub`, `email`, `name` and `jti`. Roles are read from the database on each request through authorization policies (`OrganizationPolicies`, `RequireProjectPermission`). Users who cannot see an organization or project get `404`, so their existence is not revealed; users who lack a permission get `403`.

### Consequences
- A role change or removal applies immediately.
- Authorization costs a database query per request; accepted, and it can be cached very briefly if needed.
- The permission matrix lives in `Domain/Projects/ProjectPermissions.cs`; changing it means updating `ProjectPermissionsTests` and `ProjectAuthorizationTests` on purpose.

### Review when
If request volume makes the per-request lookup a measured bottleneck.

## Español

### Contexto
Los usuarios tienen roles distintos por organización y por proyecto, y esos roles cambian. Si los roles estuvieran en el JWT, un usuario removido o degradado conservaría su acceso anterior hasta que el token venciera.

### Opciones consideradas
1. Poner los roles en el token de acceso: sin consulta a la base de datos por solicitud, pero con permisos desactualizados.
2. Resolver los roles desde la base de datos en cada solicitud: una consulta extra, siempre al día.

### Decisión
Los tokens de acceso llevan solo `sub`, `email`, `name` y `jti`. Los roles se leen de la base de datos en cada solicitud mediante políticas de autorización (`OrganizationPolicies`, `RequireProjectPermission`). Quien no puede ver una organización o proyecto recibe `404`, para no revelar que existe; quien no tiene un permiso recibe `403`.

### Consecuencias
- Un cambio de rol o una remoción aplica de inmediato.
- La autorización cuesta una consulta por solicitud; se acepta y, si hace falta, puede usar una caché muy corta.
- La matriz de permisos vive en `Domain/Projects/ProjectPermissions.cs`; cambiarla exige actualizar a propósito `ProjectPermissionsTests` y `ProjectAuthorizationTests`.

### Revisar cuando
Si el volumen de solicitudes convierte la consulta por solicitud en un cuello de botella medido.
