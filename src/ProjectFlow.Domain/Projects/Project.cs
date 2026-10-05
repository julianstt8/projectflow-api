using ProjectFlow.Domain.Common;

namespace ProjectFlow.Domain.Projects;

public sealed class Project : Entity
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 2000;

    private readonly List<ProjectMember> _members = [];

    private Project(Guid id, Guid organizationId, ProjectKey key, string name, string? description, DateTimeOffset createdAt)
        : base(id)
    {
        OrganizationId = organizationId;
        Key = key;
        Name = name;
        Description = description;
        NextTaskNumber = 1;
        CreatedAt = createdAt;
    }

    public Guid OrganizationId { get; }

    public ProjectKey Key { get; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    /// <summary>Number for the next task (RF-04). Persistence must update it atomically.</summary>
    public int NextTaskNumber { get; private set; }

    public bool IsArchived { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public bool IsDeleted => DeletedAt is not null;

    public IReadOnlyCollection<ProjectMember> Members => _members.AsReadOnly();

    /// <summary>Creates a project. The creator becomes its first project manager.</summary>
    public static Result<Project> Create(
        Guid organizationId,
        ProjectKey key,
        string name,
        string? description,
        Guid creatorUserId,
        DateTimeOffset now)
    {
        if (organizationId == Guid.Empty)
        {
            return ProjectErrors.OrganizationRequired;
        }

        if (creatorUserId == Guid.Empty)
        {
            return ProjectErrors.UserRequired;
        }

        var details = ValidateDetails(name, description);
        if (details.IsFailure)
        {
            return details.Error;
        }

        var (validName, validDescription) = details.Value;
        var project = new Project(Guid.CreateVersion7(now), organizationId, key, validName, validDescription, now);
        project._members.Add(new ProjectMember(project.Id, creatorUserId, ProjectRole.ProjectManager));

        return project;
    }

    public string FormatTaskKey(int number) => $"{Key.Value}-{number}";

    public ProjectRole? GetMemberRole(Guid userId) => FindMember(userId)?.Role;

    public Result UpdateDetails(string name, string? description)
    {
        var canChange = EnsureCanBeChanged();
        if (canChange.IsFailure)
        {
            return canChange;
        }

        var details = ValidateDetails(name, description);
        if (details.IsFailure)
        {
            return details.Error;
        }

        (Name, Description) = details.Value;
        return Result.Success();
    }

    public Result Archive()
    {
        var canChange = EnsureCanBeChanged();
        if (canChange.IsFailure)
        {
            return canChange;
        }

        IsArchived = true;
        return Result.Success();
    }

    public Result Unarchive()
    {
        if (IsDeleted)
        {
            return ProjectErrors.Deleted;
        }

        if (!IsArchived)
        {
            return ProjectErrors.NotArchived;
        }

        IsArchived = false;
        return Result.Success();
    }

    /// <summary>Soft delete: the project is hidden but kept in the database.</summary>
    public Result Delete(DateTimeOffset now)
    {
        if (IsDeleted)
        {
            return ProjectErrors.Deleted;
        }

        DeletedAt = now;
        return Result.Success();
    }

    public Result AddMember(Guid userId, ProjectRole role)
    {
        var canChange = EnsureCanBeChanged();
        if (canChange.IsFailure)
        {
            return canChange;
        }

        if (userId == Guid.Empty)
        {
            return ProjectErrors.UserRequired;
        }

        if (!Enum.IsDefined(role))
        {
            return ProjectErrors.RoleInvalid;
        }

        if (FindMember(userId) is not null)
        {
            return ProjectErrors.MemberAlreadyExists;
        }

        _members.Add(new ProjectMember(Id, userId, role));
        return Result.Success();
    }

    public Result ChangeMemberRole(Guid userId, ProjectRole role)
    {
        var canChange = EnsureCanBeChanged();
        if (canChange.IsFailure)
        {
            return canChange;
        }

        if (!Enum.IsDefined(role))
        {
            return ProjectErrors.RoleInvalid;
        }

        var member = FindMember(userId);
        if (member is null)
        {
            return ProjectErrors.MemberNotFound;
        }

        if (role != ProjectRole.ProjectManager && IsLastProjectManager(member))
        {
            return ProjectErrors.LastProjectManager;
        }

        member.ChangeRole(role);
        return Result.Success();
    }

    public Result RemoveMember(Guid userId)
    {
        var canChange = EnsureCanBeChanged();
        if (canChange.IsFailure)
        {
            return canChange;
        }

        var member = FindMember(userId);
        if (member is null)
        {
            return ProjectErrors.MemberNotFound;
        }

        if (IsLastProjectManager(member))
        {
            return ProjectErrors.LastProjectManager;
        }

        _members.Remove(member);
        return Result.Success();
    }

    /// <summary>Archived or deleted projects accept no changes, nor new sprints, epics, tasks or labels.</summary>
    internal Result EnsureCanBeChanged()
    {
        if (IsDeleted)
        {
            return ProjectErrors.Deleted;
        }

        if (IsArchived)
        {
            return ProjectErrors.Archived;
        }

        return Result.Success();
    }

    internal int AllocateTaskNumber() => NextTaskNumber++;

    private ProjectMember? FindMember(Guid userId) => _members.Find(member => member.UserId == userId);

    private bool IsLastProjectManager(ProjectMember member) =>
        member.Role == ProjectRole.ProjectManager
        && _members.Count(m => m.Role == ProjectRole.ProjectManager) == 1;

    private static Result<(string Name, string? Description)> ValidateDetails(string? name, string? description)
    {
        var validName = Text.Required(name, NameMaxLength, ProjectErrors.NameRequired, ProjectErrors.NameTooLong);
        if (validName.IsFailure)
        {
            return validName.Error;
        }

        var validDescription = Text.Optional(description, DescriptionMaxLength, ProjectErrors.DescriptionTooLong);
        if (validDescription.IsFailure)
        {
            return validDescription.Error;
        }

        return (validName.Value, validDescription.Value);
    }
}
