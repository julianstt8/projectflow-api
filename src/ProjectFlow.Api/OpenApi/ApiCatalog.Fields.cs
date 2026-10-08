using ProjectFlow.Application.Authentication.Register;
using ProjectFlow.Application.Common;
using ProjectFlow.Application.Tasks;
using ProjectFlow.Domain.Comments;
using ProjectFlow.Domain.Epics;
using ProjectFlow.Domain.Labels;
using ProjectFlow.Domain.Organizations;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Sprints;
using ProjectFlow.Domain.Tasks;
using ProjectFlow.Domain.Users;

namespace ProjectFlow.Api.OpenApi;

internal static partial class ApiCatalog
{
    private static readonly Localized OrganizationRoleField = new(
        "`Admin` (manages members and every project) or `Member` (sees only the projects they are added to).",
        "`Admin` (gestiona miembros y todos los proyectos) o `Member` (solo ve los proyectos a los que se le añade).");

    private static readonly Localized ProjectRoleField = new(
        "`ProjectManager` (manages the project), `Developer` (creates tasks and changes their own) or `Viewer` (read only).",
        "`ProjectManager` (gestiona el proyecto), `Developer` (crea tareas y cambia las suyas) o `Viewer` (solo lectura).");

    private static readonly Localized RefreshTokenField = new(
        "The refresh token returned by login or by the last refresh.",
        "El refresh token devuelto por el login o por la última renovación.");

    private static readonly Localized TaskTypeField = new("`Story`, `Bug` or `Task`.", "`Story` (historia), `Bug` o `Task` (tarea).");

    private static readonly Localized TaskPriorityField = new(
        "`Low`, `Medium`, `High` or `Critical`.",
        "`Low` (baja), `Medium` (media), `High` (alta) o `Critical` (crítica).");

    private static readonly Localized TaskTitleField = new(
        $"Title, up to {TaskItem.TitleMaxLength} characters.",
        $"Título, hasta {TaskItem.TitleMaxLength} caracteres.");

    private static readonly Localized TaskDescriptionField = new(
        $"Optional description, up to {TaskItem.DescriptionMaxLength} characters.",
        $"Descripción opcional, hasta {TaskItem.DescriptionMaxLength} caracteres.");

    private static readonly Localized StoryPointsField = new(
        $"Optional estimate from 0 to {TaskItem.MaxStoryPoints}.",
        $"Estimación opcional de 0 a {TaskItem.MaxStoryPoints}.");

    private static readonly Localized ProjectNameField = new(
        $"Project name, up to {Project.NameMaxLength} characters.",
        $"Nombre del proyecto, hasta {Project.NameMaxLength} caracteres.");

    private static readonly Localized ProjectDescriptionField = new(
        $"Optional description, up to {Project.DescriptionMaxLength} characters.",
        $"Descripción opcional, hasta {Project.DescriptionMaxLength} caracteres.");

    /// <summary>Request bodies (<c>Schema</c>) and their fields (<c>Schema.field</c>), by component name.</summary>
    public static readonly IReadOnlyDictionary<string, Localized> Fields = new Dictionary<string, Localized>
    {
        ["RegisterUserCommand"] = new("Data of a new account.", "Datos de una cuenta nueva."),
        ["RegisterUserCommand.email"] = new("E-mail to log in with; it must be unique.", "E-mail para iniciar sesión; debe ser único."),
        ["RegisterUserCommand.password"] = new(
            $"{RegisterUserCommandValidator.PasswordMinLength}-{RegisterUserCommandValidator.PasswordMaxLength} characters with at least one letter and one digit.",
            $"De {RegisterUserCommandValidator.PasswordMinLength} a {RegisterUserCommandValidator.PasswordMaxLength} caracteres con al menos una letra y un dígito."),
        ["RegisterUserCommand.fullName"] = new(
            $"Name shown to the team, up to {User.FullNameMaxLength} characters.",
            $"Nombre que ve el equipo, hasta {User.FullNameMaxLength} caracteres."),

        ["LoginCommand"] = new("Credentials of an account.", "Credenciales de una cuenta."),
        ["LoginCommand.email"] = new("E-mail of the account (case does not matter).", "E-mail de la cuenta (da igual mayúsculas o minúsculas)."),
        ["LoginCommand.password"] = new("Password of the account.", "Contraseña de la cuenta."),

        ["RefreshTokenCommand"] = new("A refresh token to exchange.", "Un refresh token para cambiar."),
        ["RefreshTokenCommand.refreshToken"] = RefreshTokenField,
        ["LogoutCommand"] = new("The refresh token of the session to close.", "El refresh token de la sesión que se cierra."),
        ["LogoutCommand.refreshToken"] = RefreshTokenField,

        ["CreateOrganizationCommand"] = new("Data of a new organization.", "Datos de una organización nueva."),
        ["CreateOrganizationCommand.name"] = new(
            $"Display name, up to {Organization.NameMaxLength} characters.",
            $"Nombre visible, hasta {Organization.NameMaxLength} caracteres."),
        ["CreateOrganizationCommand.slug"] = new(
            $"Short unique name: {Slug.MinLength}-{Slug.MaxLength} lowercase letters, digits or single hyphens.",
            $"Nombre corto y único: de {Slug.MinLength} a {Slug.MaxLength} letras minúsculas, dígitos o guiones sueltos."),
        ["AddMemberRequest"] = new("A registered user to add to the organization.", "Un usuario registrado para añadir a la organización."),
        ["AddMemberRequest.email"] = new("E-mail the user registered with.", "E-mail con el que se registró el usuario."),
        ["AddMemberRequest.role"] = OrganizationRoleField,
        ["ChangeMemberRoleRequest"] = new("New organization role of a member.", "Nuevo rol de un miembro en la organización."),
        ["ChangeMemberRoleRequest.role"] = OrganizationRoleField,

        ["CreateProjectRequest"] = new("Data of a new project.", "Datos de un proyecto nuevo."),
        ["CreateProjectRequest.key"] = new(
            $"Prefix of the task keys (`WEB` in `WEB-12`): {ProjectKey.MinLength}-{ProjectKey.MaxLength} uppercase letters or digits, starting with a letter, unique in the organization.",
            $"Prefijo de las claves de tarea (`WEB` en `WEB-12`): de {ProjectKey.MinLength} a {ProjectKey.MaxLength} letras mayúsculas o dígitos, empezando por letra, único en la organización."),
        ["CreateProjectRequest.name"] = ProjectNameField,
        ["CreateProjectRequest.description"] = ProjectDescriptionField,
        ["UpdateProjectRequest"] = new("New name and description of a project.", "Nuevo nombre y descripción de un proyecto."),
        ["UpdateProjectRequest.name"] = ProjectNameField,
        ["UpdateProjectRequest.description"] = ProjectDescriptionField,
        ["AddProjectMemberRequest"] = new("A member of the organization to add to the project.", "Un miembro de la organización para añadir al proyecto."),
        ["AddProjectMemberRequest.email"] = new("E-mail of a member of the organization.", "E-mail de un miembro de la organización."),
        ["AddProjectMemberRequest.role"] = ProjectRoleField,
        ["ChangeProjectMemberRoleRequest"] = new("New project role of a member.", "Nuevo rol de un miembro en el proyecto."),
        ["ChangeProjectMemberRoleRequest.role"] = ProjectRoleField,

        ["SprintRequest"] = new("Data of a sprint.", "Datos de un sprint."),
        ["SprintRequest.name"] = new($"Sprint name, up to {Sprint.NameMaxLength} characters.", $"Nombre del sprint, hasta {Sprint.NameMaxLength} caracteres."),
        ["SprintRequest.goal"] = new($"Optional goal, up to {Sprint.GoalMaxLength} characters.", $"Objetivo opcional, hasta {Sprint.GoalMaxLength} caracteres."),
        ["SprintRequest.startDate"] = new(
            "Optional start date (`YYYY-MM-DD`); if empty, the day the sprint starts.",
            "Fecha de inicio opcional (`AAAA-MM-DD`); si está vacía, el día en que se inicia el sprint."),
        ["SprintRequest.endDate"] = new(
            "Optional end date (`YYYY-MM-DD`), not earlier than the start date.",
            "Fecha de fin opcional (`AAAA-MM-DD`), no anterior a la de inicio."),

        ["EpicRequest"] = new("Data of an epic.", "Datos de una épica."),
        ["EpicRequest.name"] = new($"Epic name, up to {Epic.NameMaxLength} characters.", $"Nombre de la épica, hasta {Epic.NameMaxLength} caracteres."),
        ["EpicRequest.description"] = new(
            $"Optional description, up to {Epic.DescriptionMaxLength} characters.",
            $"Descripción opcional, hasta {Epic.DescriptionMaxLength} caracteres."),

        ["CreateTaskRequest"] = new("Data of a new task. You are its reporter.", "Datos de una tarea nueva. Tú quedas como informador."),
        ["CreateTaskRequest.type"] = TaskTypeField,
        ["CreateTaskRequest.title"] = TaskTitleField,
        ["CreateTaskRequest.description"] = TaskDescriptionField,
        ["CreateTaskRequest.priority"] = TaskPriorityField,
        ["CreateTaskRequest.storyPoints"] = StoryPointsField,
        ["CreateTaskRequest.assigneeId"] = new(
            "Optional assignee: an admin, project manager or developer of the project.",
            "Responsable opcional: un administrador, jefe de proyecto o desarrollador del proyecto."),
        ["CreateTaskRequest.sprintId"] = new(
            "Optional sprint of the project that is not completed; empty means the backlog.",
            "Sprint opcional del proyecto que no esté completado; vacío significa el backlog."),
        ["CreateTaskRequest.epicId"] = new("Optional open epic of the project.", "Épica abierta opcional del proyecto."),
        ["UpdateTaskRequest"] = new("New details of a task.", "Nuevos datos de una tarea."),
        ["UpdateTaskRequest.type"] = TaskTypeField,
        ["UpdateTaskRequest.title"] = TaskTitleField,
        ["UpdateTaskRequest.description"] = TaskDescriptionField,
        ["UpdateTaskRequest.priority"] = TaskPriorityField,
        ["UpdateTaskRequest.storyPoints"] = StoryPointsField,
        ["AssignTaskRequest"] = new("New assignee of a task.", "Nuevo responsable de una tarea."),
        ["AssignTaskRequest.assigneeId"] = new(
            "Someone who works in the project, or `null` to unassign.",
            "Alguien que trabaja en el proyecto, o `null` para desasignar."),
        ["MoveTaskToSprintRequest"] = new("New sprint of a task.", "Nuevo sprint de una tarea."),
        ["MoveTaskToSprintRequest.sprintId"] = new(
            "A sprint of the project that is not completed, or `null` for the backlog.",
            "Un sprint del proyecto que no esté completado, o `null` para el backlog."),
        ["SetTaskEpicRequest"] = new("New epic of a task.", "Nueva épica de una tarea."),
        ["SetTaskEpicRequest.epicId"] = new("An open epic of the project, or `null` to unlink.", "Una épica abierta del proyecto, o `null` para desvincular."),
        ["ChangeTaskStatusRequest"] = new("Next status of a task.", "Siguiente estado de una tarea."),
        ["ChangeTaskStatusRequest.status"] = new(
            "`ToDo`, `InProgress`, `Review` or `Done`; only the next step of the workflow, or `Review` → `InProgress`.",
            "`ToDo`, `InProgress`, `Review` o `Done`; solo el siguiente paso del flujo, o `Review` → `InProgress`."),

        ["CommentRequest"] = new("Text of a comment.", "Texto de un comentario."),
        ["CommentRequest.body"] = new(
            $"The comment, up to {Comment.BodyMaxLength} characters, in any language (accents, ñ and emoji are kept).",
            $"El comentario, hasta {Comment.BodyMaxLength} caracteres, en cualquier idioma (se conservan tildes, ñ y emoji)."),
        ["LabelRequest"] = new("Data of a label.", "Datos de una etiqueta."),
        ["LabelRequest.name"] = new(
            $"Label name, unique in the project ignoring case, up to {Label.NameMaxLength} characters.",
            $"Nombre de la etiqueta, único en el proyecto sin distinguir mayúsculas, hasta {Label.NameMaxLength} caracteres."),
        ["LabelRequest.color"] = new("Hex color such as `#1D76DB`.", "Color hexadecimal como `#1D76DB`."),
    };

    /// <summary>Route and query parameters, by <c>location:name</c>.</summary>
    public static readonly IReadOnlyDictionary<string, Localized> Parameters = new Dictionary<string, Localized>(StringComparer.OrdinalIgnoreCase)
    {
        ["path:organizationId"] = new("Id of the organization (from `GET /api/organizations`).", "Id de la organización (de `GET /api/organizations`)."),
        ["path:projectId"] = new("Id of the project.", "Id del proyecto."),
        ["path:taskId"] = new("Id of the task (not its key).", "Id de la tarea (no su clave)."),
        ["path:sprintId"] = new("Id of the sprint.", "Id del sprint."),
        ["path:epicId"] = new("Id of the epic.", "Id de la épica."),
        ["path:labelId"] = new("Id of the label.", "Id de la etiqueta."),
        ["path:commentId"] = new("Id of the comment.", "Id del comentario."),
        ["path:userId"] = new("Id of the user.", "Id del usuario."),

        ["query:status"] = new(
            "Only these statuses; repeat it for several (`status=ToDo&status=Review`).",
            "Solo estos estados; repítelo para varios (`status=ToDo&status=Review`)."),
        ["query:assigneeId"] = new("Only tasks assigned to this user.", "Solo tareas asignadas a este usuario."),
        ["query:unassigned"] = new("`true`: only tasks without assignee.", "`true`: solo tareas sin responsable."),
        ["query:sprintId"] = new("Only tasks of this sprint.", "Solo tareas de este sprint."),
        ["query:backlog"] = new("`true`: only tasks outside any sprint.", "`true`: solo tareas fuera de cualquier sprint."),
        ["query:epicId"] = new("Only tasks of this epic.", "Solo tareas de esta épica."),
        ["query:labelId"] = new("Only tasks with this label.", "Solo tareas con esta etiqueta."),
        ["query:q"] = new(
            $"Words of the title, ignoring case and accents, or a task key or number (`WEB-12`, `12`). Up to {SearchTasksQueryValidator.TextMaxLength} characters.",
            $"Palabras del título, sin distinguir mayúsculas ni tildes, o una clave o número de tarea (`WEB-12`, `12`). Hasta {SearchTasksQueryValidator.TextMaxLength} caracteres."),
        ["query:sort"] = new(
            $"`{TaskSort.Number}` (default), `{TaskSort.NumberDescending}`, `{TaskSort.UpdatedAt}` or `{TaskSort.UpdatedAtDescending}` (a leading `-` means descending).",
            $"`{TaskSort.Number}` (por defecto), `{TaskSort.NumberDescending}`, `{TaskSort.UpdatedAt}` o `{TaskSort.UpdatedAtDescending}` (un `-` delante significa descendente)."),
        ["query:page"] = new("Page number, from 1.", "Número de página, desde 1."),
        ["query:pageSize"] = new(
            $"Results per page, from 1 to {PagedResponse<object>.MaxPageSize}.",
            $"Resultados por página, de 1 a {PagedResponse<object>.MaxPageSize}."),
        ["query:entityId"] = new(
            "Only the history of one item: a task, sprint, epic, comment or the project itself.",
            "Solo la historia de un elemento: una tarea, sprint, épica, comentario o el propio proyecto."),
    };
}
