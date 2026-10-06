using FluentValidation;
using Mediator;
using ProjectFlow.Application.Abstractions;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Users;

namespace ProjectFlow.Application.Projects;

// ---------- Create ----------

public sealed record CreateProjectCommand(Guid OrganizationId, string Key, string Name, string? Description)
    : ICommand<Result<ProjectSummaryResponse>>;

public sealed class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(command => command.Key).NotEmpty().MaximumLength(ProjectKey.MaxLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(Project.NameMaxLength);
        RuleFor(command => command.Description).MaximumLength(Project.DescriptionMaxLength);
    }
}

/// <summary>Creates a project (RF-04); the current user becomes its first project manager.</summary>
public sealed class CreateProjectCommandHandler(
    IProjectRepository projects,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<CreateProjectCommand, Result<ProjectSummaryResponse>>
{
    public async ValueTask<Result<ProjectSummaryResponse>> Handle(CreateProjectCommand command, CancellationToken cancellationToken)
    {
        var key = ProjectKey.Create(command.Key);
        if (key.IsFailure)
        {
            return key.Error;
        }

        if (await projects.ExistsWithKeyAsync(command.OrganizationId, key.Value, cancellationToken))
        {
            return ProjectUseCaseErrors.KeyAlreadyTaken;
        }

        var project = Project.Create(
            command.OrganizationId,
            key.Value,
            command.Name,
            command.Description,
            currentUser.UserId,
            timeProvider.GetUtcNow());
        if (project.IsFailure)
        {
            return project.Error;
        }

        projects.Add(project.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ProjectSummaryResponse(project.Value.Id, project.Value.Key.Value, project.Value.Name, IsArchived: false, ProjectRole.ProjectManager);
    }
}

// ---------- Update, archive, unarchive, delete ----------

public sealed record UpdateProjectCommand(Guid ProjectId, string Name, string? Description) : ICommand<Result>;

public sealed class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(Project.NameMaxLength);
        RuleFor(command => command.Description).MaximumLength(Project.DescriptionMaxLength);
    }
}

public sealed class UpdateProjectCommandHandler(IProjectRepository projects, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateProjectCommand, Result>
{
    public ValueTask<Result> Handle(UpdateProjectCommand command, CancellationToken cancellationToken) =>
        projects.ChangeAsync(unitOfWork, command.ProjectId, project => project.UpdateDetails(command.Name, command.Description), cancellationToken);
}

public sealed record ArchiveProjectCommand(Guid ProjectId) : ICommand<Result>;

public sealed class ArchiveProjectCommandHandler(IProjectRepository projects, IUnitOfWork unitOfWork)
    : ICommandHandler<ArchiveProjectCommand, Result>
{
    public ValueTask<Result> Handle(ArchiveProjectCommand command, CancellationToken cancellationToken) =>
        projects.ChangeAsync(unitOfWork, command.ProjectId, project => project.Archive(), cancellationToken);
}

public sealed record UnarchiveProjectCommand(Guid ProjectId) : ICommand<Result>;

public sealed class UnarchiveProjectCommandHandler(IProjectRepository projects, IUnitOfWork unitOfWork)
    : ICommandHandler<UnarchiveProjectCommand, Result>
{
    public ValueTask<Result> Handle(UnarchiveProjectCommand command, CancellationToken cancellationToken) =>
        projects.ChangeAsync(unitOfWork, command.ProjectId, project => project.Unarchive(), cancellationToken);
}

/// <summary>Soft delete: the project and its tasks disappear from every query but stay in the database.</summary>
public sealed record DeleteProjectCommand(Guid ProjectId) : ICommand<Result>;

public sealed class DeleteProjectCommandHandler(IProjectRepository projects, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    : ICommandHandler<DeleteProjectCommand, Result>
{
    public ValueTask<Result> Handle(DeleteProjectCommand command, CancellationToken cancellationToken) =>
        projects.ChangeAsync(unitOfWork, command.ProjectId, project => project.Delete(timeProvider.GetUtcNow()), cancellationToken);
}

// ---------- Members ----------

public sealed record AddProjectMemberCommand(Guid OrganizationId, Guid ProjectId, string Email, ProjectRole Role)
    : ICommand<Result<ProjectMemberResponse>>;

public sealed class AddProjectMemberCommandValidator : AbstractValidator<AddProjectMemberCommand>
{
    public AddProjectMemberCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty();
        RuleFor(command => command.Role).IsInEnum();
    }
}

/// <summary>Gives a project role to a member of the organization (RF-03).</summary>
public sealed class AddProjectMemberCommandHandler(
    IProjectRepository projects,
    IUserRepository users,
    IOrganizationMembership organizationMembership,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddProjectMemberCommand, Result<ProjectMemberResponse>>
{
    public async ValueTask<Result<ProjectMemberResponse>> Handle(AddProjectMemberCommand command, CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(command.ProjectId, cancellationToken);
        if (project is null)
        {
            return ProjectUseCaseErrors.NotFound;
        }

        var email = Email.Create(command.Email);
        var user = email.IsSuccess ? await users.GetByEmailAsync(email.Value, cancellationToken) : null;
        if (user is null)
        {
            return ProjectUseCaseErrors.UserNotFound;
        }

        if (await organizationMembership.GetRoleAsync(command.OrganizationId, user.Id, cancellationToken) is null)
        {
            return ProjectUseCaseErrors.UserNotInOrganization;
        }

        var added = project.AddMember(user.Id, command.Role);
        if (added.IsFailure)
        {
            return added.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ProjectMemberResponse(user.Id, user.Email.Value, user.FullName, command.Role);
    }
}

public sealed record ChangeProjectMemberRoleCommand(Guid ProjectId, Guid UserId, ProjectRole Role) : ICommand<Result>;

public sealed class ChangeProjectMemberRoleCommandValidator : AbstractValidator<ChangeProjectMemberRoleCommand>
{
    public ChangeProjectMemberRoleCommandValidator()
    {
        RuleFor(command => command.Role).IsInEnum();
    }
}

/// <summary>Every project keeps at least one project manager (domain rule, 422).</summary>
public sealed class ChangeProjectMemberRoleCommandHandler(IProjectRepository projects, IUnitOfWork unitOfWork)
    : ICommandHandler<ChangeProjectMemberRoleCommand, Result>
{
    public ValueTask<Result> Handle(ChangeProjectMemberRoleCommand command, CancellationToken cancellationToken) =>
        projects.ChangeAsync(unitOfWork, command.ProjectId, project => project.ChangeMemberRole(command.UserId, command.Role), cancellationToken);
}

public sealed record RemoveProjectMemberCommand(Guid ProjectId, Guid UserId) : ICommand<Result>;

public sealed class RemoveProjectMemberCommandHandler(IProjectRepository projects, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveProjectMemberCommand, Result>
{
    public ValueTask<Result> Handle(RemoveProjectMemberCommand command, CancellationToken cancellationToken) =>
        projects.ChangeAsync(unitOfWork, command.ProjectId, project => project.RemoveMember(command.UserId), cancellationToken);
}

// ---------- Shared ----------

internal static class ProjectRepositoryExtensions
{
    /// <summary>Loads the project, applies a domain change and saves it; 404 if the project is not visible.</summary>
    public static async ValueTask<Result> ChangeAsync(
        this IProjectRepository projects,
        IUnitOfWork unitOfWork,
        Guid projectId,
        Func<Project, Result> change,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            return ProjectUseCaseErrors.NotFound;
        }

        var result = change(project);
        if (result.IsFailure)
        {
            return result;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
