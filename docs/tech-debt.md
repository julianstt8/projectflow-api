# Technical debt register · Registro de deuda técnica

**English** | [Español](#español)

Every shortcut that is accepted on purpose is written here, so it is a decision and not a surprise. Review this list at the end of every milestone: pay the item, renew it with a reason, or drop it. Do not start a new milestone with overdue debt unless that is decided explicitly.

| What | Why it was accepted | Risk | Pay by (date or event) | Issue |
|---|---|---|---|---|
| Label names are unique per project only in the use case (no unique index) | Low impact; a case-insensitive unique index needs a functional index on `lower(name)` | Two simultaneous creations with the same name could both succeed | When labels are created concurrently in practice, or before the first public release | #18 |
| Task text search (`%word%`) cannot use a B-tree index | Projects are small in the MVP; `unaccent` + `ILIKE` is enough | Slow search on projects with many thousands of tasks | When a project passes ~10k tasks: add a `pg_trgm` GIN index on `unaccent(title)` | #20 |

---

## Español

Cada atajo que se acepta a propósito se anota aquí, para que sea una decisión y no una sorpresa. Revisa esta lista al terminar cada hito: se paga el ítem, se renueva con un motivo o se descarta. No se abre un hito nuevo con deuda vencida salvo que se decida de forma explícita.

| Qué | Por qué se aceptó | Riesgo | Pagar para (fecha o evento) | Issue |
|---|---|---|---|---|
| Los nombres de etiqueta son únicos por proyecto solo en el caso de uso (sin índice único) | Impacto bajo; un índice único sin distinguir mayúsculas requiere un índice funcional sobre `lower(name)` | Dos creaciones simultáneas con el mismo nombre podrían tener éxito ambas | Cuando se creen etiquetas a la vez en la práctica, o antes de la primera versión pública | #18 |
| La búsqueda de texto de tareas (`%palabra%`) no puede usar un índice B-tree | Los proyectos son pequeños en el MVP; `unaccent` + `ILIKE` basta | Búsqueda lenta en proyectos con muchos miles de tareas | Cuando un proyecto pase de ~10k tareas: añadir un índice GIN `pg_trgm` sobre `unaccent(title)` | #20 |
