# ADR 0006: Insert-only activity log enforced by a database trigger · Registro de actividad solo de inserción, con trigger en la base de datos

- **Date · Fecha:** 2026-10-06
- **Status · Estado:** Accepted

## English

### Context
The activity log (RF-10) must show who changed what and when, with old and new values. An audit trail that can be edited or deleted is not trustworthy, and a bug or a manual query must not be able to rewrite history.

### Options considered
1. Application-level rule only: easy to bypass by mistake.
2. Database trigger that rejects `UPDATE` and `DELETE` on the table, plus application discipline.
3. Separate append-only store: stronger, but more infrastructure than the MVP needs.

### Decision
`activity_logs` is insert-only. A database trigger rejects any update or delete. Entities raise `ProjectActivityEvent`s only when a value really changes, and `UnitOfWork` writes the rows in the same transaction as the change, so a rejected change leaves no trace. New project-scoped changes must raise an event.

### Consequences
- History cannot be altered through the application or a casual SQL statement.
- Corrections are new entries, never edits.
- Data retention and privacy erasure need a deliberate process (a migration or a controlled maintenance procedure), not a simple delete.

### Review when
If legal retention or data-erasure requirements appear, or log volume needs partitioning or archiving.

## Español

### Contexto
El registro de actividad (RF-10) debe mostrar quién cambió qué y cuándo, con valores anterior y nuevo. Una auditoría que se puede editar o borrar no es confiable, y un error o una consulta manual no deben poder reescribir la historia.

### Opciones consideradas
1. Regla solo en la aplicación: fácil de saltarse por error.
2. Trigger en la base de datos que rechaza `UPDATE` y `DELETE` en la tabla, más disciplina en la aplicación.
3. Almacén separado de solo agregado: más fuerte, pero más infraestructura de la que necesita el MVP.

### Decisión
`activity_logs` es de solo inserción. Un trigger de la base de datos rechaza cualquier actualización o borrado. Las entidades generan `ProjectActivityEvent` solo cuando un valor realmente cambia, y `UnitOfWork` escribe las filas en la misma transacción del cambio, de modo que un cambio rechazado no deja rastro. Todo cambio nuevo dentro de un proyecto debe generar un evento.

### Consecuencias
- La historia no se puede alterar desde la aplicación ni con una sentencia SQL casual.
- Las correcciones son entradas nuevas, nunca ediciones.
- La retención de datos y el borrado por privacidad exigen un proceso deliberado (una migración o un procedimiento de mantenimiento controlado), no un simple borrado.

### Revisar cuando
Si aparecen requisitos legales de retención o borrado de datos, o si el volumen del registro exige particionar o archivar.
