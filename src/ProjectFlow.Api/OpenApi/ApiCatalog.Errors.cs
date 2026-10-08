namespace ProjectFlow.Api.OpenApi;

internal static partial class ApiCatalog
{
    /// <summary>What each error <c>code</c> means for the client. The API's own messages (the <c>title</c>) stay in English.</summary>
    public static readonly IReadOnlyDictionary<string, Localized> ErrorTexts = new Dictionary<string, Localized>
    {
        // ---------- Common ----------
        ["Validation.Failed"] = new(
            "Some fields are missing or invalid; `errors` lists the messages of each field.",
            "Faltan campos o no son válidos; `errors` lista los mensajes de cada campo."),
        ["Authentication.Required"] = new(
            "No access token, or it has expired: log in or renew it with `POST /api/auth/refresh`.",
            "Falta el access token o ha caducado: inicia sesión o renuévalo con `POST /api/auth/refresh`."),
        ["Authorization.Forbidden"] = new(
            "You can see the resource, but your role does not allow this action.",
            "Puedes ver el recurso, pero tu rol no permite esta acción."),
        ["Organization.NotFound"] = new(
            "The organization does not exist or you are not a member (both look the same on purpose).",
            "La organización no existe o no eres miembro (las dos cosas se ven igual a propósito)."),
        ["Project.NotFound"] = new(
            "The project does not exist, was deleted or you cannot see it (both look the same on purpose).",
            "El proyecto no existe, fue borrado o no puedes verlo (las dos cosas se ven igual a propósito)."),

        // ---------- Authentication and users ----------
        ["User.EmailInvalid"] = new("The e-mail is not valid.", "El e-mail no es válido."),
        ["Authentication.EmailAlreadyRegistered"] = new(
            "An account with this e-mail already exists: log in instead.",
            "Ya existe una cuenta con este e-mail: inicia sesión."),
        ["Authentication.InvalidCredentials"] = new(
            "The e-mail or the password is wrong.",
            "El e-mail o la contraseña son incorrectos."),
        ["Authentication.InvalidRefreshToken"] = new(
            "The refresh token is unknown, expired, already used or its session was closed: log in again.",
            "El refresh token no existe, caducó, ya se usó o su sesión se cerró: vuelve a iniciar sesión."),

        // ---------- Organizations ----------
        ["Organization.SlugInvalid"] = new(
            "The slug must be lowercase letters, digits or single hyphens, without hyphens at the start or end.",
            "El slug debe tener letras minúsculas, dígitos o guiones sueltos, sin guiones al principio ni al final."),
        ["Organization.SlugAlreadyTaken"] = new(
            "Another organization already uses this slug.",
            "Otra organización ya usa este slug."),
        ["Organization.UserNotFound"] = new(
            "No user is registered with this e-mail: they must register first.",
            "No hay ningún usuario registrado con este e-mail: primero debe registrarse."),
        ["Organization.MemberAlreadyExists"] = new(
            "The user is already a member of the organization.",
            "El usuario ya es miembro de la organización."),
        ["Organization.MemberNotFound"] = new(
            "The user is not a member of the organization.",
            "El usuario no es miembro de la organización."),
        ["Organization.LastAdmin"] = new(
            "The last admin cannot be removed or demoted: make someone else admin first.",
            "No se puede quitar ni degradar al último administrador: haz administrador a otra persona antes."),
        ["Organization.OnlyAdminsCanRemoveOthers"] = new(
            "Members can only remove themselves; removing others needs an admin.",
            "Los miembros solo pueden quitarse a sí mismos; quitar a otros requiere un administrador."),

        // ---------- Projects ----------
        ["Project.KeyInvalid"] = new(
            "The key must be uppercase letters or digits and start with a letter.",
            "La clave debe tener letras mayúsculas o dígitos y empezar por una letra."),
        ["Project.KeyAlreadyTaken"] = new(
            "Another project of the organization already uses this key.",
            "Otro proyecto de la organización ya usa esta clave."),
        ["Project.Archived"] = new(
            "The project is archived and read-only: unarchive it first.",
            "El proyecto está archivado y es de solo lectura: desarchívalo antes."),
        ["Project.NotArchived"] = new("The project is not archived.", "El proyecto no está archivado."),
        ["Project.UserNotFound"] = new(
            "No user is registered with this e-mail.",
            "No hay ningún usuario registrado con este e-mail."),
        ["Project.UserNotInOrganization"] = new(
            "The user must be a member of the organization before joining its projects.",
            "El usuario debe ser miembro de la organización antes de unirse a sus proyectos."),
        ["Project.MemberAlreadyExists"] = new(
            "The user is already a member of the project.",
            "El usuario ya es miembro del proyecto."),
        ["Project.MemberNotFound"] = new(
            "The user is not a member of the project.",
            "El usuario no es miembro del proyecto."),
        ["Project.LastProjectManager"] = new(
            "The project must keep at least one project manager.",
            "El proyecto debe conservar al menos un jefe de proyecto."),

        // ---------- Sprints ----------
        ["Sprint.NotFound"] = new("The sprint does not exist in this project.", "El sprint no existe en este proyecto."),
        ["Sprint.EndBeforeStart"] = new(
            "The end date is earlier than the start date (when starting without a start date, today counts as the start).",
            "La fecha de fin es anterior a la de inicio (al iniciar sin fecha de inicio, cuenta hoy como inicio)."),
        ["Sprint.Completed"] = new("The sprint is completed and cannot change.", "El sprint está completado y no puede cambiar."),
        ["Sprint.NotPlanned"] = new("Only a planned sprint can be started.", "Solo se puede iniciar un sprint planificado."),
        ["Sprint.AnotherSprintActive"] = new(
            "The project already has an active sprint: complete it first.",
            "El proyecto ya tiene un sprint activo: complétalo antes."),
        ["Sprint.NotActive"] = new("Only the active sprint can be completed.", "Solo se puede completar el sprint activo."),

        // ---------- Epics ----------
        ["Epic.NotFound"] = new("The epic does not exist in this project.", "La épica no existe en este proyecto."),
        ["Epic.AlreadyClosed"] = new("The epic is already closed.", "La épica ya está cerrada."),
        ["Epic.AlreadyOpen"] = new("The epic is already open.", "La épica ya está abierta."),

        // ---------- Labels ----------
        ["Label.NotFound"] = new("The label does not exist in this project.", "La etiqueta no existe en este proyecto."),
        ["Label.ColorInvalid"] = new("The color must be a hex value such as `#1D76DB`.", "El color debe ser un valor hexadecimal como `#1D76DB`."),
        ["Label.NameTaken"] = new(
            "The project already has a label with this name (ignoring case).",
            "El proyecto ya tiene una etiqueta con este nombre (sin distinguir mayúsculas)."),
        ["Label.NotInProject"] = new("The label does not exist in this project.", "La etiqueta no existe en este proyecto."),

        // ---------- Tasks ----------
        ["Task.NotFound"] = new("The task does not exist in this project.", "La tarea no existe en este proyecto."),
        ["Task.NotYourTask"] = new(
            "Developers can only change tasks they reported or are assigned to.",
            "Los desarrolladores solo pueden cambiar tareas que crearon o tienen asignadas."),
        ["Task.Closed"] = new("The task is done: reopen it before changing it.", "La tarea está terminada: reábrela antes de cambiarla."),
        ["Task.AssigneeNotAllowed"] = new(
            "The assignee must be an admin, project manager or developer of the project (viewers cannot be assigned).",
            "El responsable debe ser administrador, jefe de proyecto o desarrollador del proyecto (a los observadores no se les asignan tareas)."),
        ["Task.SprintNotInProject"] = new("The sprint does not exist in this project.", "El sprint no existe en este proyecto."),
        ["Task.SprintCompleted"] = new("Tasks cannot be moved into a completed sprint.", "No se pueden mover tareas a un sprint completado."),
        ["Task.EpicNotInProject"] = new("The epic does not exist in this project.", "La épica no existe en este proyecto."),
        ["Task.EpicClosed"] = new("Tasks cannot be added to a closed epic.", "No se pueden añadir tareas a una épica cerrada."),
        ["Task.InvalidTransition"] = new(
            "The workflow does not allow this status change (the message says from which status to which).",
            "El flujo no permite este cambio de estado (el mensaje indica de qué estado a cuál)."),
        ["Task.NotDone"] = new("Only a done task can be reopened.", "Solo se puede reabrir una tarea terminada."),

        // ---------- Comments ----------
        ["Comment.NotFound"] = new("The comment does not exist on this task.", "El comentario no existe en esta tarea."),
        ["Comment.NotAuthor"] = new("Only the author can edit a comment.", "Solo el autor puede editar un comentario."),
        ["Comment.CannotDelete"] = new(
            "Only the author, a project manager or an organization admin can delete a comment.",
            "Solo el autor, un jefe de proyecto o un administrador de la organización pueden borrar un comentario."),
    };
}
