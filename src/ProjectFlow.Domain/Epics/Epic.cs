using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Domain.Epics;

public sealed class Epic : Entity, IOrganizationOwned
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 5000;

    private Epic(Guid id, Guid projectId, Guid organizationId, string name, string? description)
        : base(id)
    {
        ProjectId = projectId;
        OrganizationId = organizationId;
        Name = name;
        Description = description;
        Status = EpicStatus.Open;
    }

    public Guid ProjectId { get; }

    public Guid OrganizationId { get; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public EpicStatus Status { get; private set; }

    public static Result<Epic> Create(Project project, string name, string? description, DateTimeOffset now)
    {
        var canChange = project.EnsureCanBeChanged();
        if (canChange.IsFailure)
        {
            return canChange.Error;
        }

        var details = ValidateDetails(name, description);
        if (details.IsFailure)
        {
            return details.Error;
        }

        var (validName, validDescription) = details.Value;
        return new Epic(Guid.CreateVersion7(now), project.Id, project.OrganizationId, validName, validDescription);
    }

    public Result UpdateDetails(string name, string? description)
    {
        var details = ValidateDetails(name, description);
        if (details.IsFailure)
        {
            return details.Error;
        }

        (Name, Description) = details.Value;
        return Result.Success();
    }

    public Result Close()
    {
        if (Status == EpicStatus.Closed)
        {
            return EpicErrors.AlreadyClosed;
        }

        Status = EpicStatus.Closed;
        return Result.Success();
    }

    public Result Reopen()
    {
        if (Status == EpicStatus.Open)
        {
            return EpicErrors.AlreadyOpen;
        }

        Status = EpicStatus.Open;
        return Result.Success();
    }

    private static Result<(string Name, string? Description)> ValidateDetails(string? name, string? description)
    {
        var validName = Text.Required(name, NameMaxLength, EpicErrors.NameRequired, EpicErrors.NameTooLong);
        if (validName.IsFailure)
        {
            return validName.Error;
        }

        var validDescription = Text.Optional(description, DescriptionMaxLength, EpicErrors.DescriptionTooLong);
        if (validDescription.IsFailure)
        {
            return validDescription.Error;
        }

        return (validName.Value, validDescription.Value);
    }
}
