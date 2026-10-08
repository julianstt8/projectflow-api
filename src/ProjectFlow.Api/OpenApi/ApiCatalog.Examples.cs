using ProjectFlow.Api.Controllers;
using ProjectFlow.Application.Activity;
using ProjectFlow.Application.Authentication;
using ProjectFlow.Application.Authentication.Login;
using ProjectFlow.Application.Authentication.Logout;
using ProjectFlow.Application.Authentication.Refresh;
using ProjectFlow.Application.Authentication.Register;
using ProjectFlow.Application.Comments;
using ProjectFlow.Application.Common;
using ProjectFlow.Application.Epics;
using ProjectFlow.Application.Labels;
using ProjectFlow.Application.Organizations;
using ProjectFlow.Application.Projects;
using ProjectFlow.Application.Sprints;
using ProjectFlow.Application.Tasks;
using ProjectFlow.Domain.Epics;
using ProjectFlow.Domain.Organizations;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Sprints;
using ProjectFlow.Domain.Tasks;

namespace ProjectFlow.Api.OpenApi;

internal static partial class ApiCatalog
{
    private static readonly Guid Ana = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
    private static readonly Guid Carla = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
    private static readonly Guid Acme = Guid.Parse("3d6f4b2a-1c8e-4f7a-9b2d-5e6f7a8b9c0d");
    private static readonly Guid Web = Guid.Parse("a1b2c3d4-e5f6-4a5b-8c7d-9e0f1a2b3c4d");
    private static readonly Guid Sprint3 = Guid.Parse("b2c3d4e5-f6a7-4b6c-9d8e-0f1a2b3c4d5e");
    private static readonly Guid AuthEpic = Guid.Parse("c3d4e5f6-a7b8-4c7d-8e9f-1a2b3c4d5e6f");
    private static readonly Guid Web12 = Guid.Parse("d4e5f6a7-b8c9-4d8e-9fa0-2b3c4d5e6f7a");
    private static readonly Guid Backend = Guid.Parse("e5f6a7b8-c9d0-4e9f-8a1b-3c4d5e6f7a8b");
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 14, 30, 0, TimeSpan.Zero);

    private static readonly TaskResponse Task = new(
        Web12, "WEB-12", 12, "Login fails with uppercase e-mail", "Steps: log in with ANA@EXAMPLE.COM.", TaskType.Bug,
        TaskPriority.Critical, TaskItemStatus.InProgress, 2, Ana, Carla, Sprint3, AuthEpic, [Backend], Now.AddDays(-2), Now);

    /// <summary>
    /// Realistic request and response bodies shown in the reference (requests are prefilled when trying an endpoint).
    /// They are instances of the real types, so they cannot drift from the API.
    /// </summary>
    public static readonly IReadOnlyDictionary<Type, object> Examples = new Dictionary<Type, object>
    {
        // Requests
        [typeof(RegisterUserCommand)] = new RegisterUserCommand("ana.admin@example.com", "ProjectFlow-Dev-2026", "Ana Admin"),
        [typeof(LoginCommand)] = new LoginCommand("ana.admin@example.com", "ProjectFlow-Dev-2026"),
        [typeof(RefreshTokenCommand)] = new RefreshTokenCommand("hT8sN0q3vX1kP5wZ2mR7cY4bL9dF6gJ0aE3uI8oQ1tS"),
        [typeof(LogoutCommand)] = new LogoutCommand("hT8sN0q3vX1kP5wZ2mR7cY4bL9dF6gJ0aE3uI8oQ1tS"),
        [typeof(CreateOrganizationCommand)] = new CreateOrganizationCommand("Acme Software", "acme-software"),
        [typeof(AddMemberRequest)] = new AddMemberRequest("carla.dev@example.com", OrganizationRole.Member),
        [typeof(ChangeMemberRoleRequest)] = new ChangeMemberRoleRequest(OrganizationRole.Admin),
        [typeof(CreateProjectRequest)] = new CreateProjectRequest("WEB", "ProjectFlow Web", "Customer-facing web application."),
        [typeof(UpdateProjectRequest)] = new UpdateProjectRequest("ProjectFlow Web", "Customer-facing web application and landing page."),
        [typeof(AddProjectMemberRequest)] = new AddProjectMemberRequest("carla.dev@example.com", ProjectRole.Developer),
        [typeof(ChangeProjectMemberRoleRequest)] = new ChangeProjectMemberRoleRequest(ProjectRole.ProjectManager),
        [typeof(SprintRequest)] = new SprintRequest("Sprint 3", "Password recovery", new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 25)),
        [typeof(EpicRequest)] = new EpicRequest("Authentication", "Sign-up, login and password recovery."),
        [typeof(CreateTaskRequest)] = new CreateTaskRequest(
            TaskType.Bug, "Login fails with uppercase e-mail", "Steps: log in with ANA@EXAMPLE.COM.", TaskPriority.Critical, 2, null, null, null),
        [typeof(UpdateTaskRequest)] = new UpdateTaskRequest(TaskType.Bug, "Login fails with uppercase e-mail", null, TaskPriority.High, 3),
        [typeof(AssignTaskRequest)] = new AssignTaskRequest(Carla),
        [typeof(MoveTaskToSprintRequest)] = new MoveTaskToSprintRequest(Sprint3),
        [typeof(SetTaskEpicRequest)] = new SetTaskEpicRequest(AuthEpic),
        [typeof(ChangeTaskStatusRequest)] = new ChangeTaskStatusRequest(TaskItemStatus.InProgress),
        [typeof(CommentRequest)] = new CommentRequest("¡Corregido! La comparación de correos ya no distingue mayúsculas."),
        [typeof(LabelRequest)] = new LabelRequest("backend", "#1D76DB"),

        // Responses
        [typeof(UserResponse)] = new UserResponse(Ana, "ana.admin@example.com", "Ana Admin"),
        [typeof(TokenResponse)] = new TokenResponse(
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9…", "Bearer", Now.AddMinutes(15),
            "hT8sN0q3vX1kP5wZ2mR7cY4bL9dF6gJ0aE3uI8oQ1tS", Now.AddDays(7)),
        [typeof(CurrentUserResponse)] = new CurrentUserResponse(Ana, "ana.admin@example.com", "Ana Admin"),
        [typeof(OrganizationSummaryResponse)] = new OrganizationSummaryResponse(Acme, "Acme Software", "acme-software", OrganizationRole.Admin),
        [typeof(OrganizationMemberResponse)] = new OrganizationMemberResponse(Carla, "carla.dev@example.com", "Carla Dev", OrganizationRole.Member, Now),
        [typeof(OrganizationDetailsResponse)] = new OrganizationDetailsResponse(
            Acme, "Acme Software", "acme-software", Now.AddMonths(-3),
            [
                new(Ana, "ana.admin@example.com", "Ana Admin", OrganizationRole.Admin, Now.AddMonths(-3)),
                new(Carla, "carla.dev@example.com", "Carla Dev", OrganizationRole.Member, Now.AddMonths(-1)),
            ]),
        [typeof(ProjectSummaryResponse)] = new ProjectSummaryResponse(Web, "WEB", "ProjectFlow Web", false, ProjectRole.ProjectManager),
        [typeof(ProjectMemberResponse)] = new ProjectMemberResponse(Carla, "carla.dev@example.com", "Carla Dev", ProjectRole.Developer),
        [typeof(ProjectDetailsResponse)] = new ProjectDetailsResponse(
            Web, "WEB", "ProjectFlow Web", "Customer-facing web application.", false, Now.AddMonths(-2),
            [
                new(Ana, "ana.admin@example.com", "Ana Admin", ProjectRole.ProjectManager),
                new(Carla, "carla.dev@example.com", "Carla Dev", ProjectRole.Developer),
            ]),
        [typeof(SprintResponse)] = new SprintResponse(
            Sprint3, "Sprint 3", "Password recovery", new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 25), SprintStatus.Active, 8),
        [typeof(EpicResponse)] = new EpicResponse(AuthEpic, "Authentication", "Sign-up, login and password recovery.", EpicStatus.Open, 5, 3),
        [typeof(TaskResponse)] = Task,
        [typeof(PagedResponse<TaskResponse>)] = new PagedResponse<TaskResponse>([Task], 1, 25, 1),
        [typeof(LabelResponse)] = new LabelResponse(Backend, "backend", "#1D76DB"),
        [typeof(CommentResponse)] = new CommentResponse(
            Guid.Parse("f6a7b8c9-d0e1-4fa0-9b2c-4d5e6f7a8b9c"), Web12, Carla, "Carla Dev",
            "¡Corregido! La comparación de correos ya no distingue mayúsculas.", Now),
        [typeof(PagedResponse<ActivityResponse>)] = new PagedResponse<ActivityResponse>(
            [new(Guid.Parse("a7b8c9d0-e1f2-4ab1-8c3d-5e6f7a8b9c0d"), Carla, "Carla Dev", "Task", Web12, "StatusChanged", "ToDo", "InProgress", Now)],
            1, 50, 1),
    };
}
