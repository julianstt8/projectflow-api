using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectFlow.Application.Abstractions;
using ProjectFlow.Domain.Comments;
using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Epics;
using ProjectFlow.Domain.Labels;
using ProjectFlow.Domain.Organizations;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Sprints;
using ProjectFlow.Domain.Tasks;
using ProjectFlow.Domain.Users;

namespace ProjectFlow.Infrastructure.Persistence.Seeding;

/// <summary>
/// Fake, clearly fictional data for local development and the public demo. Everything is built through the domain
/// model, so the seed obeys the same rules as the API. Runs only when configured (<c>Database:SeedOnStartup</c>, or the
/// demo reset of ADR 0010) and only once.
/// </summary>
internal sealed class DevelopmentDataSeeder(
    ApplicationDbContext dbContext,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider,
    ILogger<DevelopmentDataSeeder> logger)
{
    /// <summary>
    /// Password of every seeded user. Published on purpose (README): these accounts exist only in local development and
    /// in the public demo, whose data is fictional and restored every day. Never seed them in a real environment.
    /// </summary>
    public const string Password = "ProjectFlow-Dev-2026";

    public const string AcmeSlug = "acme-software";
    public const string GlobexSlug = "globex";

    private readonly List<TaskItem> _tasks = [];
    private readonly List<Comment> _comments = [];
    private DateTimeOffset _now;

    /// <returns><see langword="true"/> if data was inserted; <see langword="false"/> if it was already there.</returns>
    public async Task<bool> SeedAsync(CancellationToken cancellationToken = default)
    {
        var acmeSlug = Ok(Slug.Create(AcmeSlug));
        if (await dbContext.Organizations.AnyAsync(organization => organization.Slug == acmeSlug, cancellationToken))
        {
            logger.LogInformation("Development seed data already present; skipping");
            return false;
        }

        _now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(_now.UtcDateTime);

        // Users: one per role, plus Carla, who belongs to both organizations (RF-02).
        var ana = NewUser("ana.admin@example.com", "Ana Admin");
        var bruno = NewUser("bruno.pm@example.com", "Bruno Project Manager");
        var carla = NewUser("carla.dev@example.com", "Carla Developer");
        var diego = NewUser("diego.dev@example.com", "Diego Developer");
        var elena = NewUser("elena.viewer@example.com", "Elena Viewer");
        var frank = NewUser("frank.admin@example.com", "Frank Admin");
        var grace = NewUser("grace.dev@example.com", "Grace Developer");

        var acme = Ok(Organization.Create("Acme Software", acmeSlug, ana.Id, _now));
        foreach (var member in new[] { bruno, carla, diego, elena })
        {
            Ok(acme.AddMember(member.Id, OrganizationRole.Member, _now));
        }

        var globex = Ok(Organization.Create("Globex Corporation", Ok(Slug.Create(GlobexSlug)), frank.Id, _now));
        Ok(globex.AddMember(grace.Id, OrganizationRole.Member, _now));
        Ok(globex.AddMember(carla.Id, OrganizationRole.Member, _now));

        // Acme · ProjectFlow Web: a full sprint cycle.
        var web = NewProject(acme, "WEB", "ProjectFlow Web", "Customer-facing web application.", bruno);
        Ok(web.AddMember(carla.Id, ProjectRole.Developer));
        Ok(web.AddMember(diego.Id, ProjectRole.Developer));
        Ok(web.AddMember(elena.Id, ProjectRole.Viewer));

        var sprint1 = Ok(Sprint.Create(web, "Sprint 1", "Sign-up and login", today.AddDays(-21), today.AddDays(-8), _now));
        var sprint2 = Ok(Sprint.Create(web, "Sprint 2", "Project dashboard", today.AddDays(-7), today.AddDays(6), _now));
        var sprint3 = Ok(Sprint.Create(web, "Sprint 3", "Password recovery", today.AddDays(7), today.AddDays(20), _now));

        var authentication = Ok(Epic.Create(web, "Authentication", "Sign-up, login and password recovery.", _now));
        var dashboard = Ok(Epic.Create(web, "Dashboard", "Project overview for managers.", _now));
        var legacy = Ok(Epic.Create(web, "Legacy cleanup", "Remove the old front-end code.", _now));

        var backend = Ok(Label.Create(web, "backend", "#1D76DB", _now));
        var frontend = Ok(Label.Create(web, "frontend", "#0E8A16", _now));
        var bug = Ok(Label.Create(web, "bug", "#D73A4A", _now));
        var ux = Ok(Label.Create(web, "ux", "#A2EEEF", _now));

        NewTask(web, bruno, TaskType.Story, "Create sign-up endpoint", TaskPriority.High, 5, carla, sprint1, authentication, [backend], TaskItemStatus.Done);
        NewTask(web, bruno, TaskType.Story, "Build login form", TaskPriority.High, 3, diego, sprint1, authentication, [frontend], TaskItemStatus.Done);
        NewTask(web, carla, TaskType.Task, "Hash passwords with PBKDF2", TaskPriority.Critical, 2, carla, sprint1, authentication, [backend], TaskItemStatus.Done);
        NewTask(web, diego, TaskType.Task, "Remove old jQuery widgets", TaskPriority.Low, 3, diego, sprint1, legacy, [frontend], TaskItemStatus.Done);

        var overview = NewTask(web, bruno, TaskType.Story, "Project overview page", TaskPriority.High, 5, diego, sprint2, dashboard, [frontend, ux], TaskItemStatus.InProgress);
        NewTask(web, bruno, TaskType.Story, "Tasks by status chart", TaskPriority.Medium, 3, null, sprint2, dashboard, [frontend, ux], TaskItemStatus.ToDo);
        NewTask(web, bruno, TaskType.Task, "Project statistics endpoint", TaskPriority.Medium, 3, carla, sprint2, dashboard, [backend], TaskItemStatus.Review);
        var loginBug = NewTask(web, elena, TaskType.Bug, "Login fails with uppercase e-mail", TaskPriority.Critical, 1, carla, sprint2, authentication, [backend, bug], TaskItemStatus.InProgress);

        NewTask(web, bruno, TaskType.Story, "Password reset by e-mail", TaskPriority.Medium, 5, null, sprint3, authentication, [backend], TaskItemStatus.ToDo);
        NewTask(web, diego, TaskType.Story, "Dark mode", TaskPriority.Low, null, null, null, null, [frontend, ux], TaskItemStatus.ToDo);
        var duplicate = NewTask(web, elena, TaskType.Bug, "Duplicate: login page is slow", TaskPriority.Low, null, null, null, null, [], TaskItemStatus.ToDo);
        Ok(duplicate.Delete(_now));

        NewComment(overview, bruno, "Please follow the dashboard mockups shared in the design review.");
        NewComment(overview, diego, "Layout is done; wiring the statistics endpoint next.");
        NewComment(loginBug, carla, "Reproduced: e-mails were compared case-sensitively.");

        Ok(legacy.Close());
        Ok(sprint1.Start([sprint1, sprint2, sprint3], sprint1.StartDate!.Value));
        Ok(sprint1.Complete(sprint1.EndDate!.Value));
        Ok(sprint2.Start([sprint1, sprint2, sprint3], today));

        // Acme · ProjectFlow Mobile: backlog only.
        var mobile = NewProject(acme, "MOB", "ProjectFlow Mobile", "iOS and Android application.", bruno);
        Ok(mobile.AddMember(carla.Id, ProjectRole.Developer));
        NewTask(mobile, bruno, TaskType.Task, "Set up the mobile project", TaskPriority.High, 2, carla, null, null, [], TaskItemStatus.ToDo);
        NewTask(mobile, bruno, TaskType.Story, "Push notifications for assigned tasks", TaskPriority.Medium, 8, null, null, null, [], TaskItemStatus.ToDo);

        // Globex · Data Platform: a second organization to try isolation.
        var platform = NewProject(globex, "DATA", "Data Platform", "Internal analytics pipeline.", frank);
        Ok(platform.AddMember(grace.Id, ProjectRole.Developer));
        Ok(platform.AddMember(carla.Id, ProjectRole.Viewer));
        var globexSprint = Ok(Sprint.Create(platform, "Sprint 1", "First data ingestion", today.AddDays(-3), today.AddDays(10), _now));
        var pipeline = Ok(Label.Create(platform, "pipeline", "#5319E7", _now));
        var ingest = NewTask(platform, frank, TaskType.Story, "Ingest events from the message queue", TaskPriority.High, 8, grace, globexSprint, null, [pipeline], TaskItemStatus.InProgress);
        NewTask(platform, frank, TaskType.Story, "Daily revenue report", TaskPriority.Medium, 5, null, globexSprint, null, [pipeline], TaskItemStatus.ToDo);
        NewComment(ingest, frank, "Try it on the staging cluster first.");
        Ok(globexSprint.Start([globexSprint], today));

        dbContext.AddRange(ana, bruno, carla, diego, elena, frank, grace);
        dbContext.AddRange(acme, globex);
        dbContext.AddRange(web, mobile, platform);
        dbContext.AddRange(sprint1, sprint2, sprint3, globexSprint);
        dbContext.AddRange(authentication, dashboard, legacy);
        dbContext.AddRange(backend, frontend, bug, ux, pipeline);
        dbContext.AddRange(_tasks);
        dbContext.AddRange(_comments);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded development data: 2 organizations, 7 users, 3 projects, {TaskCount} tasks",
            _tasks.Count);
        return true;
    }

    private User NewUser(string email, string fullName) =>
        Ok(User.Create(Ok(Email.Create(email)), passwordHasher.Hash(Password), fullName, _now));

    private Project NewProject(Organization organization, string key, string name, string description, User manager) =>
        Ok(Project.Create(organization.Id, Ok(ProjectKey.Create(key)), name, description, manager.Id, _now));

    private TaskItem NewTask(
        Project project,
        User reporter,
        TaskType type,
        string title,
        TaskPriority priority,
        int? storyPoints,
        User? assignee,
        Sprint? sprint,
        Epic? epic,
        Label[] labels,
        TaskItemStatus status)
    {
        var task = Ok(TaskItem.Create(project, reporter.Id, type, title, null, priority, _now));
        Ok(task.Estimate(storyPoints, _now));
        Ok(task.Assign(assignee?.Id, _now));
        Ok(task.MoveToSprint(sprint, _now));
        Ok(task.SetEpic(epic, _now));
        foreach (var label in labels)
        {
            Ok(task.AddLabel(label, _now));
        }

        TaskItemStatus[] workflow = [TaskItemStatus.InProgress, TaskItemStatus.Review, TaskItemStatus.Done];
        foreach (var next in workflow.TakeWhile(_ => task.Status != status))
        {
            Ok(task.ChangeStatus(next, _now));
        }

        _tasks.Add(task);
        return task;
    }

    private void NewComment(TaskItem task, User author, string body) =>
        _comments.Add(Ok(Comment.Create(task, author.Id, body, _now)));

    private static T Ok<T>(Result<T> result) =>
        result.IsSuccess ? result.Value : throw InvalidSeed(result.Error);

    private static void Ok(Result result)
    {
        if (result.IsFailure)
        {
            throw InvalidSeed(result.Error);
        }
    }

    private static InvalidOperationException InvalidSeed(Error error) =>
        new($"Development seed data breaks a domain rule: {error.Code} ({error.Description})");
}
