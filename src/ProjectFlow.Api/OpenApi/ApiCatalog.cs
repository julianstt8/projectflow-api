using ProjectFlow.Domain.Common;

namespace ProjectFlow.Api.OpenApi;

/// <summary>Reference texts of one operation and the errors of its use case (authorization errors are added by <see cref="EndpointAccess"/>).</summary>
internal sealed record OperationText(Localized Title, Localized Description, params Error[] Errors);

/// <summary>A group of endpoints (one controller) in the reference.</summary>
internal sealed record TagText(string Controller, Localized Name, Localized Description);

/// <summary>Groups of tags shown in the sidebar of the reference (<c>x-tagGroups</c>).</summary>
internal sealed record TagGroupText(Localized Name, params string[] Controllers);

/// <summary>
/// Every text of the API reference, English and Spanish side by side. The OpenAPI transformers apply the
/// language of each document; <c>ApiReferenceTests</c> fails when an entry is missing in either language.
/// XML comments stay as documentation of the code; these texts are written for the readers of the API.
/// </summary>
internal static partial class ApiCatalog
{
    public static readonly Localized Description = new(
        "Software project management API (mini-Jira): organizations, projects, sprints, epics, tasks, comments, labels, " +
        "activity log and search, with role-based access control.\n\n" +
        "**Getting started**\n" +
        "1. `POST /api/auth/login` (or register first) and copy the `accessToken`.\n" +
        "2. Paste it as the Bearer token (it lasts 15 minutes; renew it with `POST /api/auth/refresh`).\n" +
        "3. List your organizations with `GET /api/organizations` and use their ids in the other routes.\n\n" +
        "**Errors** are `application/problem+json` with a stable `code` to branch on and a `traceId` to find the request in the logs. " +
        "Each endpoint lists the codes it can return.",
        "API de gestión de proyectos de software (mini-Jira): organizaciones, proyectos, sprints, épicas, tareas, comentarios, " +
        "etiquetas, registro de actividad y búsqueda, con control de acceso por roles.\n\n" +
        "**Para empezar**\n" +
        "1. `POST /api/auth/login` (o regístrate antes) y copia el `accessToken`.\n" +
        "2. Pégalo como token Bearer (dura 15 minutos; renuévalo con `POST /api/auth/refresh`).\n" +
        "3. Lista tus organizaciones con `GET /api/organizations` y usa sus ids en las demás rutas.\n\n" +
        "**Los errores** son `application/problem+json` con un `code` estable para decidir qué hacer y un `traceId` para encontrar " +
        "la petición en los logs. Cada endpoint lista los códigos que puede devolver.");

    public static readonly Localized BearerDescription = new(
        "Access token from `POST /api/auth/login` (valid 15 minutes).",
        "Access token de `POST /api/auth/login` (válido 15 minutos).");

    public static readonly Localized WhoCanCallLabel = new("Who can call it", "Quién puede llamarlo");

    public static readonly Localized PossibleCodes = new("Possible `code` values:", "Valores posibles de `code`:");

    /// <summary>Tags in the order the reference shows them.</summary>
    public static readonly IReadOnlyList<TagText> Tags =
    [
        new("Auth",
            new("Authentication", "Autenticación"),
            new("Register, log in and keep the session alive. The access token lasts 15 minutes; the refresh token changes on every use " +
                "and reusing an old one closes the session.",
                "Registrarse, iniciar sesión y mantenerla. El access token dura 15 minutos; el refresh token cambia en cada uso y " +
                "reutilizar uno viejo cierra la sesión.")),
        new("Organizations",
            new("Organizations", "Organizaciones"),
            new("Workspaces that own the projects. Whoever creates one is its first admin; admins manage the members. " +
                "Users who are not members get 404, as if the organization did not exist.",
                "Espacios de trabajo dueños de los proyectos. Quien crea una es su primer administrador; los administradores gestionan " +
                "a los miembros. Quien no es miembro recibe 404, como si la organización no existiera.")),
        new("Projects",
            new("Projects", "Proyectos"),
            new("Projects of an organization, their members and project roles: project manager, developer and viewer. " +
                "Organization admins can do everything in every project. Archived projects are read-only.",
                "Proyectos de una organización, sus miembros y roles de proyecto: jefe de proyecto, desarrollador y observador. " +
                "Los administradores de la organización pueden hacer todo en todos los proyectos. Los proyectos archivados son de solo lectura.")),
        new("Sprints",
            new("Sprints", "Sprints"),
            new("Time boxes of work: planned → active → completed. Only one sprint per project can be active; completing it sends " +
                "the unfinished tasks back to the backlog.",
                "Periodos de trabajo: planificado → activo → completado. Solo puede haber un sprint activo por proyecto; al completarlo, " +
                "las tareas sin terminar vuelven al backlog.")),
        new("Epics",
            new("Epics", "Épicas"),
            new("Large features that group tasks, with their progress. Closed epics accept no new tasks.",
                "Funcionalidades grandes que agrupan tareas, con su progreso. Las épicas cerradas no aceptan tareas nuevas.")),
        new("Tasks",
            new("Tasks", "Tareas"),
            new("Stories, bugs and tasks. Each one gets a key such as `WEB-12` and follows the workflow " +
                "`ToDo` → `InProgress` → `Review` → `Done`. Search with filters, sorting and pages.",
                "Historias, bugs y tareas. Cada una recibe una clave como `WEB-12` y sigue el flujo " +
                "`ToDo` → `InProgress` → `Review` → `Done`. Búsqueda con filtros, orden y páginas.")),
        new("Labels",
            new("Labels", "Etiquetas"),
            new("Colored labels of a project, and tagging tasks with them.",
                "Etiquetas de color de un proyecto, y etiquetar tareas con ellas.")),
        new("Comments",
            new("Comments", "Comentarios"),
            new("Conversation on a task. Any language, accents and emoji are kept as written.",
                "Conversación sobre una tarea. Cualquier idioma, tildes y emoji se guardan tal como se escriben.")),
        new("Activity",
            new("Activity", "Actividad"),
            new("Audit log of the project: who changed what and when. It cannot be edited or deleted.",
                "Registro de auditoría del proyecto: quién cambió qué y cuándo. No se puede editar ni borrar.")),
    ];

    public static TagText TagOf(string controller) => Tags.Single(tag => tag.Controller == controller);

    public static readonly IReadOnlyList<TagGroupText> TagGroups =
    [
        new(new("Access", "Acceso"), "Auth", "Organizations"),
        new(new("Planning", "Planificación"), "Projects", "Sprints", "Epics"),
        new(new("Work", "Trabajo"), "Tasks", "Labels", "Comments", "Activity"),
    ];

    /// <summary>Description of each response status, before its error codes.</summary>
    public static readonly IReadOnlyDictionary<int, Localized> Statuses = new Dictionary<int, Localized>
    {
        [StatusCodes.Status200OK] = new("Success.", "Correcto."),
        [StatusCodes.Status201Created] = new("Created.", "Creado."),
        [StatusCodes.Status204NoContent] = new("Done, no content.", "Hecho, sin contenido."),
        [StatusCodes.Status400BadRequest] = new("**Invalid request.**", "**Petición no válida.**"),
        [StatusCodes.Status401Unauthorized] = new("**Not authenticated.**", "**Sin autenticar.**"),
        [StatusCodes.Status403Forbidden] = new("**Not allowed.**", "**Sin permiso.**"),
        [StatusCodes.Status404NotFound] = new("**Not found.**", "**No encontrado.**"),
        [StatusCodes.Status409Conflict] = new("**Conflict with existing data.**", "**Conflicto con datos existentes.**"),
        [StatusCodes.Status422UnprocessableEntity] = new("**Business rule broken.**", "**Regla de negocio incumplida.**"),
    };
}
