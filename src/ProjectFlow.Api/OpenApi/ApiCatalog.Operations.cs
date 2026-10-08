using ProjectFlow.Application.Authentication;
using ProjectFlow.Application.Comments;
using ProjectFlow.Application.Epics;
using ProjectFlow.Application.Labels;
using ProjectFlow.Application.Organizations;
using ProjectFlow.Application.Projects;
using ProjectFlow.Application.Sprints;
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
    /// <summary>Texts of every operation, by <c>Controller.Action</c>, with the errors its use case can return.</summary>
    public static readonly IReadOnlyDictionary<string, OperationText> Operations = new Dictionary<string, OperationText>
    {
        // ---------- Authentication ----------
        ["Auth.Register"] = new(
            new("Register", "Registrarse"),
            new("Creates a user account. It does not log in: call `POST /api/auth/login` afterwards to get the tokens.",
                "Crea una cuenta de usuario. No inicia sesión: llama después a `POST /api/auth/login` para obtener los tokens."),
            UserErrors.EmailInvalid, AuthenticationErrors.EmailAlreadyRegistered),
        ["Auth.Login"] = new(
            new("Log in", "Iniciar sesión"),
            new("Returns an access token (15 minutes) to send as `Authorization: Bearer <token>`, and a refresh token to get new ones " +
                "without logging in again. A wrong e-mail and a wrong password give the same error, so nobody can find out which e-mails exist.",
                "Devuelve un access token (15 minutos) para enviar como `Authorization: Bearer <token>` y un refresh token para obtener " +
                "otros sin volver a iniciar sesión. Un e-mail o una contraseña incorrectos dan el mismo error, para que nadie pueda " +
                "averiguar qué e-mails existen."),
            AuthenticationErrors.InvalidCredentials),
        ["Auth.Refresh"] = new(
            new("Renew the tokens", "Renovar los tokens"),
            new("Exchanges a refresh token for a new access token and a new refresh token. Each refresh token works **once**: " +
                "reusing an old one is treated as theft and closes the whole session, so log in again.",
                "Cambia un refresh token por un access token y un refresh token nuevos. Cada refresh token sirve **una sola vez**: " +
                "reutilizar uno viejo se trata como un robo y cierra toda la sesión, así que hay que volver a iniciar sesión."),
            AuthenticationErrors.InvalidRefreshToken),
        ["Auth.Logout"] = new(
            new("Log out", "Cerrar sesión"),
            new("Closes the session of the refresh token: it and the tokens renewed from it stop working. " +
                "It always answers 204, even if the token was already invalid.",
                "Cierra la sesión del refresh token: deja de funcionar, igual que los tokens renovados a partir de él. " +
                "Siempre responde 204, aunque el token ya no fuera válido.")),
        ["Auth.Me"] = new(
            new("Current user", "Usuario actual"),
            new("The user identified by the access token. Useful to check that the token works.",
                "El usuario identificado por el access token. Sirve para comprobar que el token funciona.")),

        // ---------- Organizations ----------
        ["Organizations.Create"] = new(
            new("Create an organization", "Crear una organización"),
            new($"Creates an organization with you as its first admin. The slug is its short unique name in URLs: {Slug.MinLength}-{Slug.MaxLength} " +
                "lowercase letters, digits or single hyphens (e.g. `acme-software`).",
                $"Crea una organización contigo como primer administrador. El slug es su nombre corto y único en URLs: de {Slug.MinLength} a " +
                $"{Slug.MaxLength} letras minúsculas, dígitos o guiones sueltos (p. ej. `acme-software`)."),
            OrganizationErrors.SlugInvalid, OrganizationUseCaseErrors.SlugAlreadyTaken),
        ["Organizations.List"] = new(
            new("My organizations", "Mis organizaciones"),
            new("The organizations you belong to, with your role in each one. Start here to get the ids for the other routes.",
                "Las organizaciones a las que perteneces, con tu rol en cada una. Empieza aquí para obtener los ids de las demás rutas.")),
        ["Organizations.Get"] = new(
            new("Get an organization", "Ver una organización"),
            new("The organization with all its members and their roles.",
                "La organización con todos sus miembros y sus roles.")),
        ["Organizations.AddMember"] = new(
            new("Add a member", "Añadir un miembro"),
            new("Adds an already registered user, by e-mail, as `Admin` or `Member`. Members only see the projects they are added to; " +
                "admins see and manage every project.",
                "Añade a un usuario ya registrado, por su e-mail, como `Admin` o `Member`. Los miembros solo ven los proyectos a los que " +
                "se les añade; los administradores ven y gestionan todos los proyectos."),
            OrganizationUseCaseErrors.UserNotFound, OrganizationErrors.MemberAlreadyExists),
        ["Organizations.ChangeMemberRole"] = new(
            new("Change a member's role", "Cambiar el rol de un miembro"),
            new("Makes a member `Admin` or `Member`. The organization always keeps at least one admin.",
                "Convierte a un miembro en `Admin` o `Member`. La organización siempre conserva al menos un administrador."),
            OrganizationErrors.MemberNotFound, OrganizationErrors.LastAdmin),
        ["Organizations.RemoveMember"] = new(
            new("Remove a member", "Quitar un miembro"),
            new("Admins can remove anyone; any member can remove themselves (leave the organization). The last admin cannot leave.",
                "Los administradores pueden quitar a cualquiera; cualquier miembro puede quitarse a sí mismo (salir de la organización). " +
                "El último administrador no puede salir."),
            OrganizationUseCaseErrors.OnlyAdminsCanRemoveOthers, OrganizationErrors.MemberNotFound, OrganizationErrors.LastAdmin),

        // ---------- Projects ----------
        ["Projects.Create"] = new(
            new("Create a project", "Crear un proyecto"),
            new($"Creates a project. Its key ({ProjectKey.MinLength}-{ProjectKey.MaxLength} uppercase letters or digits, starting with a letter) " +
                "prefixes the task keys: `WEB` gives `WEB-1`, `WEB-2`… It cannot be changed later.",
                $"Crea un proyecto. Su clave (de {ProjectKey.MinLength} a {ProjectKey.MaxLength} letras mayúsculas o dígitos, empezando por letra) " +
                "es el prefijo de las claves de tarea: `WEB` da `WEB-1`, `WEB-2`… No se puede cambiar después."),
            ProjectErrors.KeyInvalid, ProjectUseCaseErrors.KeyAlreadyTaken),
        ["Projects.List"] = new(
            new("List projects", "Listar proyectos"),
            new("The projects you can see in the organization, sorted by name, with your role in each one (admins see all of them).",
                "Los proyectos que puedes ver en la organización, ordenados por nombre, con tu rol en cada uno (los administradores ven todos).")),
        ["Projects.Get"] = new(
            new("Get a project", "Ver un proyecto"),
            new("The project with its members and their project roles.",
                "El proyecto con sus miembros y sus roles en el proyecto.")),
        ["Projects.Update"] = new(
            new("Edit a project", "Editar un proyecto"),
            new("Changes the name and description. The key does not change.",
                "Cambia el nombre y la descripción. La clave no cambia."),
            ProjectErrors.Archived),
        ["Projects.Archive"] = new(
            new("Archive a project", "Archivar un proyecto"),
            new("Makes the project read-only: everything can still be read, nothing can be changed until it is unarchived.",
                "Deja el proyecto en solo lectura: todo se puede seguir leyendo, nada se puede cambiar hasta desarchivarlo."),
            ProjectErrors.Archived),
        ["Projects.Unarchive"] = new(
            new("Unarchive a project", "Desarchivar un proyecto"),
            new("Allows changes in an archived project again.",
                "Vuelve a permitir cambios en un proyecto archivado."),
            ProjectErrors.NotArchived),
        ["Projects.Delete"] = new(
            new("Delete a project", "Borrar un proyecto"),
            new("Hides the project and everything in it. The data is kept in the database (soft delete) and the activity log stays.",
                "Oculta el proyecto y todo su contenido. Los datos se conservan en la base de datos (borrado lógico) y el registro de " +
                "actividad se mantiene."),
            ProjectUseCaseErrors.NotFound),
        ["Projects.AddMember"] = new(
            new("Add a project member", "Añadir un miembro al proyecto"),
            new("Adds a member of the organization, by e-mail, as `ProjectManager`, `Developer` or `Viewer`. " +
                "Project managers manage everything in the project; developers create tasks and change their own; viewers only read.",
                "Añade a un miembro de la organización, por su e-mail, como `ProjectManager`, `Developer` o `Viewer`. " +
                "Los jefes de proyecto gestionan todo el proyecto; los desarrolladores crean tareas y cambian las suyas; los observadores solo leen."),
            ProjectUseCaseErrors.UserNotFound, ProjectErrors.MemberAlreadyExists, ProjectUseCaseErrors.UserNotInOrganization, ProjectErrors.Archived),
        ["Projects.ChangeMemberRole"] = new(
            new("Change a project role", "Cambiar un rol del proyecto"),
            new("Changes the role of a project member. The project always keeps at least one project manager.",
                "Cambia el rol de un miembro del proyecto. El proyecto siempre conserva al menos un jefe de proyecto."),
            ProjectErrors.MemberNotFound, ProjectErrors.LastProjectManager, ProjectErrors.Archived),
        ["Projects.RemoveMember"] = new(
            new("Remove a project member", "Quitar un miembro del proyecto"),
            new("Removes someone from the project; they stay in the organization. The last project manager cannot be removed.",
                "Quita a alguien del proyecto; sigue en la organización. No se puede quitar al último jefe de proyecto."),
            ProjectErrors.MemberNotFound, ProjectErrors.LastProjectManager, ProjectErrors.Archived),

        // ---------- Sprints ----------
        ["Sprints.List"] = new(
            new("List sprints", "Listar sprints"),
            new("The sprints of the project with their status and number of tasks.",
                "Los sprints del proyecto con su estado y número de tareas.")),
        ["Sprints.Get"] = new(
            new("Get a sprint", "Ver un sprint"),
            new("A sprint with its status and number of tasks.",
                "Un sprint con su estado y número de tareas."),
            SprintUseCaseErrors.NotFound),
        ["Sprints.Create"] = new(
            new("Create a sprint", "Crear un sprint"),
            new("Creates a planned sprint. Dates are optional; the end date cannot be earlier than the start date.",
                "Crea un sprint planificado. Las fechas son opcionales; la fecha de fin no puede ser anterior a la de inicio."),
            SprintErrors.EndBeforeStart, ProjectErrors.Archived),
        ["Sprints.Update"] = new(
            new("Edit a sprint", "Editar un sprint"),
            new("Changes the name, goal and dates of a planned or active sprint. Completed sprints cannot change.",
                "Cambia el nombre, el objetivo y las fechas de un sprint planificado o activo. Los sprints completados no cambian."),
            SprintUseCaseErrors.NotFound, SprintErrors.EndBeforeStart, SprintErrors.Completed, ProjectErrors.Archived),
        ["Sprints.Start"] = new(
            new("Start a sprint", "Iniciar un sprint"),
            new("Starts a planned sprint (today, if it had no start date). Only one sprint per project can be active at a time.",
                "Inicia un sprint planificado (hoy, si no tenía fecha de inicio). Solo puede haber un sprint activo a la vez por proyecto."),
            SprintUseCaseErrors.NotFound, SprintErrors.EndBeforeStart, SprintErrors.AnotherSprintActive, SprintErrors.NotPlanned, ProjectErrors.Archived),
        ["Sprints.Complete"] = new(
            new("Complete a sprint", "Completar un sprint"),
            new("Completes the active sprint. Done tasks stay in it; unfinished tasks go back to the backlog.",
                "Completa el sprint activo. Las tareas terminadas se quedan en él; las que no están terminadas vuelven al backlog."),
            SprintUseCaseErrors.NotFound, SprintErrors.NotActive, ProjectErrors.Archived),

        // ---------- Epics ----------
        ["Epics.List"] = new(
            new("List epics", "Listar épicas"),
            new("The epics of the project with their progress (tasks and done tasks).",
                "Las épicas del proyecto con su progreso (tareas y tareas terminadas).")),
        ["Epics.Get"] = new(
            new("Get an epic", "Ver una épica"),
            new("An epic with its progress.", "Una épica con su progreso."),
            EpicUseCaseErrors.NotFound),
        ["Epics.Create"] = new(
            new("Create an epic", "Crear una épica"),
            new("Creates an open epic to group tasks.", "Crea una épica abierta para agrupar tareas."),
            ProjectErrors.Archived),
        ["Epics.Update"] = new(
            new("Edit an epic", "Editar una épica"),
            new("Changes the name and description of an epic.", "Cambia el nombre y la descripción de una épica."),
            EpicUseCaseErrors.NotFound, ProjectErrors.Archived),
        ["Epics.Close"] = new(
            new("Close an epic", "Cerrar una épica"),
            new("Closes the epic: tasks already linked stay, but no new tasks can be added to it.",
                "Cierra la épica: las tareas ya vinculadas se quedan, pero no se pueden añadir tareas nuevas."),
            EpicUseCaseErrors.NotFound, EpicErrors.AlreadyClosed, ProjectErrors.Archived),
        ["Epics.Reopen"] = new(
            new("Reopen an epic", "Reabrir una épica"),
            new("Reopens a closed epic so tasks can be added again.", "Reabre una épica cerrada para poder añadir tareas otra vez."),
            EpicUseCaseErrors.NotFound, EpicErrors.AlreadyOpen, ProjectErrors.Archived),

        // ---------- Tasks ----------
        ["Tasks.Search"] = new(
            new("Search tasks", "Buscar tareas"),
            new("Lists the project's tasks, paged. All filters are optional and combined. `q` matches words of the title ignoring case " +
                "and accents (`camion` finds \"Camión\"), or a key or number (`WEB-12`, `12`).",
                "Lista las tareas del proyecto, por páginas. Todos los filtros son opcionales y se combinan. `q` busca palabras del título " +
                "sin distinguir mayúsculas ni tildes (`camion` encuentra \"Camión\"), o una clave o número (`WEB-12`, `12`).")),
        ["Tasks.Get"] = new(
            new("Get a task", "Ver una tarea"),
            new("A task with its key and labels.", "Una tarea con su clave y sus etiquetas."),
            TaskUseCaseErrors.NotFound),
        ["Tasks.Create"] = new(
            new("Create a task", "Crear una tarea"),
            new("Creates a task in `ToDo` with the next key of the project (e.g. `WEB-12`); you are its reporter. " +
                "Assignee, sprint and epic are optional: without sprint it goes to the backlog.",
                "Crea una tarea en `ToDo` con la siguiente clave del proyecto (p. ej. `WEB-12`); tú quedas como informador. " +
                "Responsable, sprint y épica son opcionales: sin sprint va al backlog."),
            TaskUseCaseErrors.AssigneeNotAllowed, TaskUseCaseErrors.SprintNotInProject, TaskErrors.SprintCompleted,
            TaskUseCaseErrors.EpicNotInProject, TaskErrors.EpicClosed, ProjectErrors.Archived),
        ["Tasks.Update"] = new(
            new("Edit a task", "Editar una tarea"),
            new("Changes type, title, description, priority and story points. Done tasks must be reopened first.",
                "Cambia tipo, título, descripción, prioridad y puntos de historia. Las tareas terminadas hay que reabrirlas antes."),
            TaskUseCaseErrors.NotFound, TaskUseCaseErrors.NotYourTask, TaskErrors.Closed, ProjectErrors.Archived),
        ["Tasks.Assign"] = new(
            new("Assign a task", "Asignar una tarea"),
            new("Assigns the task to someone who works in the project (admin, project manager or developer), or unassigns it with `null`.",
                "Asigna la tarea a alguien que trabaja en el proyecto (administrador, jefe de proyecto o desarrollador), o la desasigna con `null`."),
            TaskUseCaseErrors.NotFound, TaskUseCaseErrors.NotYourTask, TaskUseCaseErrors.AssigneeNotAllowed, TaskErrors.Closed, ProjectErrors.Archived),
        ["Tasks.MoveToSprint"] = new(
            new("Move a task to a sprint", "Mover una tarea a un sprint"),
            new("Moves the task to a planned or active sprint, or back to the backlog with `null`.",
                "Mueve la tarea a un sprint planificado o activo, o de vuelta al backlog con `null`."),
            TaskUseCaseErrors.NotFound, TaskUseCaseErrors.NotYourTask, TaskUseCaseErrors.SprintNotInProject, TaskErrors.SprintCompleted,
            TaskErrors.Closed, ProjectErrors.Archived),
        ["Tasks.SetEpic"] = new(
            new("Set a task's epic", "Cambiar la épica de una tarea"),
            new("Links the task to an open epic, or unlinks it with `null`.",
                "Vincula la tarea a una épica abierta, o la desvincula con `null`."),
            TaskUseCaseErrors.NotFound, TaskUseCaseErrors.NotYourTask, TaskUseCaseErrors.EpicNotInProject, TaskErrors.EpicClosed,
            TaskErrors.Closed, ProjectErrors.Archived),
        ["Tasks.ChangeStatus"] = new(
            new("Change a task's status", "Cambiar el estado de una tarea"),
            new("Moves the task one step along `ToDo` → `InProgress` → `Review` → `Done`. The only step back is `Review` → `InProgress`; " +
                "to change a done task, reopen it.",
                "Avanza la tarea un paso en `ToDo` → `InProgress` → `Review` → `Done`. El único paso atrás es `Review` → `InProgress`; " +
                "para cambiar una tarea terminada, hay que reabrirla."),
            TaskUseCaseErrors.NotFound, TaskUseCaseErrors.NotYourTask, TaskErrors.InvalidTransition(TaskItemStatus.ToDo, TaskItemStatus.Done),
            ProjectErrors.Archived),
        ["Tasks.Reopen"] = new(
            new("Reopen a task", "Reabrir una tarea"),
            new("Moves a done task back to `InProgress` so it can be changed again.",
                "Devuelve una tarea terminada a `InProgress` para poder cambiarla otra vez."),
            TaskUseCaseErrors.NotFound, TaskErrors.NotDone, ProjectErrors.Archived),
        ["Tasks.Delete"] = new(
            new("Delete a task", "Borrar una tarea"),
            new("Hides the task (soft delete). Its key is never reused and its activity stays in the log.",
                "Oculta la tarea (borrado lógico). Su clave no se reutiliza y su actividad se queda en el registro."),
            TaskUseCaseErrors.NotFound, ProjectErrors.Archived),

        // ---------- Labels ----------
        ["Labels.List"] = new(
            new("List labels", "Listar etiquetas"),
            new("The labels of the project, sorted by name.", "Las etiquetas del proyecto, ordenadas por nombre.")),
        ["Labels.Create"] = new(
            new("Create a label", "Crear una etiqueta"),
            new("Creates a label with a name, unique in the project ignoring case, and a hex color.",
                "Crea una etiqueta con un nombre, único en el proyecto sin distinguir mayúsculas, y un color hexadecimal."),
            LabelErrors.ColorInvalid, LabelUseCaseErrors.NameTaken, ProjectErrors.Archived),
        ["Labels.Update"] = new(
            new("Edit a label", "Editar una etiqueta"),
            new("Changes the name and color of a label. Tasks keep it.", "Cambia el nombre y el color de una etiqueta. Las tareas la conservan."),
            LabelUseCaseErrors.NotFound, LabelErrors.ColorInvalid, LabelUseCaseErrors.NameTaken, ProjectErrors.Archived),
        ["Labels.Delete"] = new(
            new("Delete a label", "Borrar una etiqueta"),
            new("Deletes the label for good and removes it from every task.",
                "Borra la etiqueta definitivamente y la quita de todas las tareas."),
            LabelUseCaseErrors.NotFound, ProjectErrors.Archived),
        ["Labels.AddToTask"] = new(
            new("Add a label to a task", "Etiquetar una tarea"),
            new("Adds a label of the project to the task. Adding it twice changes nothing.",
                "Añade una etiqueta del proyecto a la tarea. Añadirla dos veces no cambia nada."),
            TaskUseCaseErrors.NotFound, TaskUseCaseErrors.NotYourTask, LabelUseCaseErrors.NotInProject, TaskErrors.Closed, ProjectErrors.Archived),
        ["Labels.RemoveFromTask"] = new(
            new("Remove a label from a task", "Quitar una etiqueta de una tarea"),
            new("Removes the label from the task. Removing one it does not have changes nothing.",
                "Quita la etiqueta de la tarea. Quitar una que no tiene no cambia nada."),
            TaskUseCaseErrors.NotFound, TaskUseCaseErrors.NotYourTask, TaskErrors.Closed, ProjectErrors.Archived),

        // ---------- Comments ----------
        ["Comments.List"] = new(
            new("List comments", "Listar comentarios"),
            new("The comments of a task, oldest first, with their authors.",
                "Los comentarios de una tarea, del más antiguo al más reciente, con sus autores."),
            TaskUseCaseErrors.NotFound),
        ["Comments.Add"] = new(
            new("Comment on a task", "Comentar una tarea"),
            new($"Adds a comment of up to {Comment.BodyMaxLength} characters as the current user.",
                $"Añade un comentario de hasta {Comment.BodyMaxLength} caracteres como el usuario actual."),
            TaskUseCaseErrors.NotFound, ProjectErrors.Archived),
        ["Comments.Edit"] = new(
            new("Edit a comment", "Editar un comentario"),
            new("Changes the text of a comment. Only its author can edit it.",
                "Cambia el texto de un comentario. Solo su autor puede editarlo."),
            TaskUseCaseErrors.NotFound, CommentUseCaseErrors.NotFound, CommentErrors.NotAuthor, ProjectErrors.Archived),
        ["Comments.Delete"] = new(
            new("Delete a comment", "Borrar un comentario"),
            new("Deletes a comment. Its author, project managers and organization admins can delete it.",
                "Borra un comentario. Pueden borrarlo su autor, los jefes de proyecto y los administradores de la organización."),
            TaskUseCaseErrors.NotFound, CommentUseCaseErrors.NotFound, CommentUseCaseErrors.CannotDelete, ProjectErrors.Archived),

        // ---------- Activity ----------
        ["Activity.List"] = new(
            new("Project activity", "Actividad del proyecto"),
            new("Who changed what and when in the project, newest first and paged. Filter by `entityId` to see the history of one task, " +
                "sprint or epic.",
                "Quién cambió qué y cuándo en el proyecto, de lo más reciente a lo más antiguo y por páginas. Filtra por `entityId` para " +
                "ver la historia de una tarea, sprint o épica.")),
    };
}
