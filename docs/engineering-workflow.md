# Engineering workflow · Flujo de desarrollo

**English** | [Español](#español)

## English

Every change follows two gates, for people and for Claude. The rules themselves live in `CLAUDE.md` (conventions), `docs/adr/` (decisions) and the shared Notion page "Estándares de Calidad y Deuda Técnica".

### Gate 1 · Before writing code
1. Read the issue and its acceptance criteria.
2. Read `CLAUDE.md` and the ADRs that touch the area you change.
3. Look for an existing pattern in the code (a similar use case, endpoint or test) and follow it.
4. Write a short plan in the issue or PR: layers touched, new permissions or errors, tests to add, migration needed, activity events to raise.
5. If the plan breaks a rule, stop: either change the plan or record the decision (new ADR, or an entry in `docs/tech-debt.md`).

### Gate 2 · After writing code (review)
Review the diff against this list before asking for a merge:
- [ ] Layers respected (architecture tests green); no business rule duplicated.
- [ ] Errors are `Result`s with the right `ErrorType`; no exceptions for expected failures.
- [ ] New endpoints have the right authorization policy / project permission.
- [ ] Project changes raise activity events; new task changes go through `TaskEditor`.
- [ ] Small functions and classes, clear names, no commented-out code, no `TODO`.
- [ ] Tests cover the new behavior, including permissions and edge cases; none are flaky.
- [ ] Model change has its migration; no secrets; warnings fixed, not suppressed.
- [ ] Docs updated (`CLAUDE.md`, README, ADR) and accepted shortcuts registered in `docs/tech-debt.md`.
- [ ] Each rule broken on purpose is explained in the PR.

CI enforces what a machine can check (format, warnings, tests, architecture, vulnerable packages, secrets). The rest is checked in review using the list above.

---

## Español

Cada cambio pasa por dos puertas, para personas y para Claude. Las reglas viven en `CLAUDE.md` (convenciones), `docs/adr/` (decisiones) y la página compartida de Notion "Estándares de Calidad y Deuda Técnica".

### Puerta 1 · Antes de escribir código
1. Leer el issue y sus criterios de aceptación.
2. Leer `CLAUDE.md` y los ADR que tocan la zona que se cambia.
3. Buscar un patrón existente en el código (un caso de uso, endpoint o prueba parecido) y seguirlo.
4. Escribir un plan corto en el issue o el PR: capas tocadas, permisos o errores nuevos, pruebas a agregar, migración necesaria, eventos de actividad.
5. Si el plan rompe una regla, detenerse: o se cambia el plan, o se registra la decisión (ADR nuevo o entrada en `docs/tech-debt.md`).

### Puerta 2 · Después de escribir código (revisión)
Revisar el cambio contra esta lista antes de pedir la unión:
- [ ] Capas respetadas (pruebas de arquitectura en verde); ninguna regla de negocio duplicada.
- [ ] Los errores son `Result` con el `ErrorType` correcto; sin excepciones para fallos esperados.
- [ ] Los endpoints nuevos tienen la política de autorización o permiso de proyecto correcto.
- [ ] Los cambios de proyecto generan eventos de actividad; los cambios de tareas pasan por `TaskEditor`.
- [ ] Funciones y clases pequeñas, nombres claros, sin código comentado ni `TODO`.
- [ ] Las pruebas cubren el comportamiento nuevo, incluidos permisos y casos límite; ninguna es intermitente.
- [ ] El cambio de modelo tiene su migración; sin secretos; advertencias corregidas, no suprimidas.
- [ ] Documentación al día (`CLAUDE.md`, README, ADR) y atajos aceptados en `docs/tech-debt.md`.
- [ ] Cada regla rota a propósito se explica en el PR.

El CI hace cumplir lo que una máquina puede verificar (formato, advertencias, pruebas, arquitectura, paquetes vulnerables, secretos). Lo demás se revisa con la lista de arriba.
