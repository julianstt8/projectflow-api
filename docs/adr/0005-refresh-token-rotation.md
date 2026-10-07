# ADR 0005: Refresh token rotation with family revocation · Rotación de refresh tokens con revocación por familia

- **Date · Fecha:** 2026-10-05
- **Status · Estado:** Accepted

## English

### Context
Access tokens are short-lived (15 minutes). Users stay signed in through a refresh token (7 days), which is a high-value secret: if it leaks, an attacker could keep a session alive.

### Options considered
1. Long-lived access tokens: simple, but a leak is dangerous for a long time.
2. Static refresh tokens: a leak goes unnoticed.
3. Rotating refresh tokens grouped in a family per login, revoking the family on reuse.

### Decision
Each login creates one token family. Every refresh returns a new pair and the used token stops working (rotation). If an already used token is presented again, the whole family is revoked. Only a SHA-256 hash of each token is stored. The refresh use case treats a lost concurrent race (`ConcurrencyConflictException`) as reuse.

### Consequences
- A stolen refresh token is detected the first time both the thief and the user use it.
- A legitimate client that retries a refresh after a network failure may be signed out; clients must not replay a refresh token.
- The signing key comes from configuration (`Jwt:SigningKey`, at least 32 characters) and is never committed.

### Review when
If the API gains a browser frontend (consider HttpOnly cookies) or if sign-out of retried refreshes proves a real problem.

## Español

### Contexto
Los tokens de acceso duran poco (15 minutos). El usuario permanece conectado con un refresh token (7 días), un secreto de alto valor: si se filtra, un atacante podría mantener una sesión viva.

### Opciones consideradas
1. Tokens de acceso de larga duración: simple, pero una filtración es peligrosa por mucho tiempo.
2. Refresh tokens estáticos: una filtración pasa inadvertida.
3. Refresh tokens rotativos agrupados en una familia por inicio de sesión, revocando la familia si se reutilizan.

### Decisión
Cada inicio de sesión crea una familia de tokens. Cada renovación devuelve un par nuevo y el token usado deja de funcionar (rotación). Si se presenta de nuevo un token ya usado, se revoca toda la familia. Solo se guarda el hash SHA-256 de cada token. El caso de uso de renovación trata una carrera concurrente perdida (`ConcurrencyConflictException`) como reutilización.

### Consecuencias
- Un refresh token robado se detecta la primera vez que lo usan tanto el ladrón como el usuario.
- Un cliente legítimo que reintenta una renovación tras un fallo de red puede quedar desconectado; los clientes no deben repetir un refresh token.
- La clave de firma viene de la configuración (`Jwt:SigningKey`, mínimo 32 caracteres) y nunca se sube al repositorio.

### Revisar cuando
Si la API suma un frontend de navegador (considerar cookies HttpOnly) o si la desconexión por reintentos resulta un problema real.
