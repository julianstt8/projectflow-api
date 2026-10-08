# ADR 0010: Public demo on Render and Neon · Demo pública en Render y Neon

- **Date · Fecha:** 2026-10-08
- **Status · Estado:** Accepted

## English

### Context
The definition of done of project 1 asks for a demo anyone can try. The API runs in Docker and needs PostgreSQL. It is a portfolio demo: no real users, no budget, fictional data. Until now the API reference, migrations on startup and demo data existed only in the Development environment.

### Options considered
1. **Azure App Service + Azure Database for PostgreSQL**: natural for .NET, but a fixed monthly cost (roughly 15–30 USD) for a demo nobody pays for.
2. **Fly.io or Railway**: simple Docker deploys, but no stable free plan.
3. **Render (free web service) + Neon (free PostgreSQL)**: no cost; Render builds the existing Dockerfile and deploys only when CI is green; Neon keeps the database (Render's own free database expires after 30 days). The service sleeps after 15 minutes without traffic and takes about a minute to wake up.

### Decision
Deploy the demo with **Render + Neon**, described in `render.yaml`. The API runs in the **Production** environment (so production checks such as the required JWT key apply) and the demo turns on, **by configuration**, what used to be tied to Development:
- `ApiReference:Enabled=true`: the Scalar reference and OpenAPI documents are public (defaults to Development only).
- `Database:MigrateOnStartup=true`: migrations run on startup.
- `Demo:ResetIntervalHours=24`: the demo data is restored once a day. A background service checks on startup and every hour, because a fixed time of day would be missed while the service sleeps. The reset empties every table, activity log included (on purpose and only here), and loads the demo data again in one transaction.
- `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`: the API sees the original HTTPS request behind Render's proxy.

The demo accounts and their password are published in the README.

### Consequences
- Anyone can try every role without registering; whatever visitors change disappears within a day.
- The first request after a quiet period takes about a minute (cold start of Render, then Neon).
- Secrets stay out of the repository: Render generates the JWT key and asks for the Neon connection string on creation.
- Public endpoints have no rate limiting yet (registered in `docs/tech-debt.md`); acceptable with fictional data and a daily reset.
- Production defaults do not change: without these settings nothing is exposed and nothing runs on startup.

### Review when
The project gets real users, needs to be always on, or the free plans change.

## Español

### Contexto
La definición de terminado del proyecto 1 pide una demo que cualquiera pueda probar. La API corre en Docker y necesita PostgreSQL. Es una demo de portafolio: sin usuarios reales, sin presupuesto, con datos ficticios. Hasta ahora la referencia de la API, las migraciones al arrancar y los datos de ejemplo solo existían en el entorno Development.

### Opciones consideradas
1. **Azure App Service + Azure Database for PostgreSQL**: natural para .NET, pero con un costo mensual fijo (aprox. 15–30 USD) para una demo que nadie paga.
2. **Fly.io o Railway**: despliegues Docker sencillos, pero sin plan gratis estable.
3. **Render (servicio web gratis) + Neon (PostgreSQL gratis)**: sin costo; Render construye el Dockerfile existente y solo despliega cuando el CI está en verde; Neon conserva la base de datos (la base gratis de Render caduca a los 30 días). El servicio se duerme tras 15 minutos sin tráfico y tarda alrededor de un minuto en despertar.

### Decisión
Desplegar la demo con **Render + Neon**, descrita en `render.yaml`. La API corre en el entorno **Production** (así aplican las comprobaciones de producción, como la clave JWT obligatoria) y la demo activa, **por configuración**, lo que antes dependía de Development:
- `ApiReference:Enabled=true`: la referencia de Scalar y los documentos OpenAPI son públicos (por defecto solo en Development).
- `Database:MigrateOnStartup=true`: las migraciones se aplican al arrancar.
- `Demo:ResetIntervalHours=24`: los datos de ejemplo se restauran una vez al día. Un servicio en segundo plano lo comprueba al arrancar y cada hora, porque una hora fija se perdería mientras el servicio duerme. El reinicio vacía todas las tablas, también el registro de actividad (a propósito y solo aquí), y vuelve a cargar los datos de ejemplo en una sola transacción.
- `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`: la API ve la petición HTTPS original detrás del proxy de Render.

Las cuentas de demo y su contraseña se publican en el README.

### Consecuencias
- Cualquiera puede probar todos los roles sin registrarse; lo que cambien los visitantes desaparece en menos de un día.
- La primera petición tras un rato sin uso tarda alrededor de un minuto (arranque en frío de Render y luego de Neon).
- Los secretos quedan fuera del repositorio: Render genera la clave JWT y pide la cadena de conexión de Neon al crear el servicio.
- Los endpoints públicos aún no tienen límite de peticiones (registrado en `docs/tech-debt.md`); aceptable con datos ficticios y reinicio diario.
- Los valores por defecto de Production no cambian: sin estas opciones no se expone nada ni se ejecuta nada al arrancar.

### Revisar cuando
El proyecto tenga usuarios reales, necesite estar siempre encendido o cambien los planes gratuitos.
