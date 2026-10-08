# Technical debt register · Registro de deuda técnica

**English** | [Español](#español)

Every shortcut that is accepted on purpose is written here, so it is a decision and not a surprise. Review this list at the end of every milestone: pay the item, renew it with a reason, or drop it. Do not start a new milestone with overdue debt unless that is decided explicitly.

| What | Why it was accepted | Risk | Pay by (date or event) | Issue |
|---|---|---|---|---|
| Task text search (`%word%`) cannot use a B-tree index | Projects are small in the MVP; `unaccent` + `ILIKE` is enough | Slow search on projects with many thousands of tasks | When a project passes ~10k tasks: add a `pg_trgm` GIN index on `unaccent(title)` | #20 |
| Public endpoints (register, login, refresh) have no rate limiting | The public demo has fictional data, published demo accounts and a daily reset | Someone could flood the demo with accounts or requests until the next reset | Before any real users, or if the demo is abused: ASP.NET Core rate limiting per IP on the auth endpoints | #73 |

**Reviews**
- 2026-10-08, end of milestone 6: label name uniqueness paid in #69 (unique index on `(project_id, lower(name))`); text search renewed, its condition (~10k tasks per project) is far from being met.

---

## Español

Cada atajo que se acepta a propósito se anota aquí, para que sea una decisión y no una sorpresa. Revisa esta lista al terminar cada hito: se paga el ítem, se renueva con un motivo o se descarta. No se abre un hito nuevo con deuda vencida salvo que se decida de forma explícita.

| Qué | Por qué se aceptó | Riesgo | Pagar para (fecha o evento) | Issue |
|---|---|---|---|---|
| La búsqueda de texto de tareas (`%palabra%`) no puede usar un índice B-tree | Los proyectos son pequeños en el MVP; `unaccent` + `ILIKE` basta | Búsqueda lenta en proyectos con muchos miles de tareas | Cuando un proyecto pase de ~10k tareas: añadir un índice GIN `pg_trgm` sobre `unaccent(title)` | #20 |
| Los endpoints públicos (registro, login, renovación) no tienen límite de peticiones | La demo pública tiene datos ficticios, cuentas de demo publicadas y un reinicio diario | Alguien podría llenar la demo de cuentas o peticiones hasta el siguiente reinicio | Antes de tener usuarios reales, o si se abusa de la demo: límite de peticiones por IP de ASP.NET Core en los endpoints de autenticación | #73 |

**Revisiones**
- 2026-10-08, cierre del hito 6: unicidad de nombres de etiqueta pagada en #69 (índice único sobre `(project_id, lower(name))`); búsqueda de texto renovada, su condición (~10k tareas por proyecto) está lejos de cumplirse.
