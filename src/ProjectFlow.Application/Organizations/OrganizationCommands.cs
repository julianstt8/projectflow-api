using FluentValidation;
using Mediator;
using ProjectFlow.Application.Abstractions;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Organizations;
using ProjectFlow.Domain.Users;

namespace ProjectFlow.Application.Organizations;

// ---------- Create ----------

public sealed record CreateOrganizationCommand(string Name, string Slug) : ICommand<Result<OrganizationSummaryResponse>>;

public sealed class CreateOrganizationCommandValidator : AbstractValidator<CreateOrganizationCommand>
{
    public CreateOrganizationCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(Organization.NameMaxLength);
        RuleFor(command => command.Slug).NotEmpty().MaximumLength(Domain.Organizations.Slug.MaxLength);
    }
}

/// <summary>Creates an organization; the current user becomes its first admin (RF-02).</summary>
public sealed class CreateOrganizationCommandHandler(
    IOrganizationRepository organizations,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<CreateOrganizationCommand, Result<OrganizationSummaryResponse>>
{
    public async ValueTask<Result<OrganizationSummaryResponse>> Handle(
        CreateOrganizationCommand command,
        CancellationToken cancellationToken)
    {
        var slug = Slug.Create(command.Slug);
        if (slug.IsFailure)
        {
            return slug.Error;
        }

        if (await organizations.ExistsWithSlugAsync(slug.Value, cancellationToken))
        {
            return OrganizationUseCaseErrors.SlugAlreadyTaken;
        }

        var organization = Organization.Create(command.Name, slug.Value, currentUser.UserId, timeProvider.GetUtcNow());
        if (organization.IsFailure)
        {
            return organization.Error;
        }

        organizations.Add(organization.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new OrganizationSummaryResponse(
            organization.Value.Id,
            organization.Value.Name,
            organization.Value.Slug.Value,
            OrganizationRole.Admin);
    }
}

// ---------- Add member ----------

public sealed record AddOrganizationMemberCommand(Guid OrganizationId, string Email, OrganizationRole Role)
    : ICommand<Result<OrganizationMemberResponse>>;

public sealed class AddOrganizationMemberCommandValidator : AbstractValidator<AddOrganizationMemberCommand>
{
    public AddOrganizationMemberCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty();
        RuleFor(command => command.Role).IsInEnum();
    }
}

/// <summary>Adds an existing user to the organization. Admin only (enforced by the endpoint policy).</summary>
public sealed class AddOrganizationMemberCommandHandler(
    IOrganizationRepository organizations,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<AddOrganizationMemberCommand, Result<OrganizationMemberResponse>>
{
    public async ValueTask<Result<OrganizationMemberResponse>> Handle(
        AddOrganizationMemberCommand command,
        CancellationToken cancellationToken)
    {
        var organization = await organizations.GetByIdAsync(command.OrganizationId, cancellationToken);
        if (organization is null)
        {
            return OrganizationUseCaseErrors.NotFound;
        }

        var email = Email.Create(command.Email);
        var user = email.IsSuccess ? await users.GetByEmailAsync(email.Value, cancellationToken) : null;
        if (user is null)
        {
            return OrganizationUseCaseErrors.UserNotFound;
        }

        var now = timeProvider.GetUtcNow();
        var added = organization.AddMember(user.Id, command.Role, now);
        if (added.IsFailure)
        {
            return added.Error;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new OrganizationMemberResponse(user.Id, user.Email.Value, user.FullName, command.Role, now);
    }
}

// ---------- Change role ----------

public sealed record ChangeOrganizationMemberRoleCommand(Guid OrganizationId, Guid UserId, OrganizationRole Role)
    : ICommand<Result>;

public sealed class ChangeOrganizationMemberRoleCommandValidator : AbstractValidator<ChangeOrganizationMemberRoleCommand>
{
    public ChangeOrganizationMemberRoleCommandValidator()
    {
        RuleFor(command => command.Role).IsInEnum();
    }
}

/// <summary>Changes a member's role. Admin only; the last admin cannot be demoted (domain rule).</summary>
public sealed class ChangeOrganizationMemberRoleCommandHandler(IOrganizationRepository organizations, IUnitOfWork unitOfWork)
    : ICommandHandler<ChangeOrganizationMemberRoleCommand, Result>
{
    public async ValueTask<Result> Handle(ChangeOrganizationMemberRoleCommand command, CancellationToken cancellationToken)
    {
        var organization = await organizations.GetByIdAsync(command.OrganizationId, cancellationToken);
        if (organization is null)
        {
            return OrganizationUseCaseErrors.NotFound;
        }

        var changed = organization.ChangeMemberRole(command.UserId, command.Role);
        if (changed.IsFailure)
        {
            return changed;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

// ---------- Remove member ----------

public sealed record RemoveOrganizationMemberCommand(Guid OrganizationId, Guid UserId) : ICommand<Result>;

/// <summary>
/// Removes a member. Admins can remove anyone; any member can leave (remove themselves).
/// The last admin can never be removed (domain rule).
/// </summary>
public sealed class RemoveOrganizationMemberCommandHandler(
    IOrganizationRepository organizations,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : ICommandHandler<RemoveOrganizationMemberCommand, Result>
{
    public async ValueTask<Result> Handle(RemoveOrganizationMemberCommand command, CancellationToken cancellationToken)
    {
        var organization = await organizations.GetByIdAsync(command.OrganizationId, cancellationToken);
        if (organization is null)
        {
            return OrganizationUseCaseErrors.NotFound;
        }

        var leaving = command.UserId == currentUser.UserId;
        if (!leaving && !organization.IsAdmin(currentUser.UserId))
        {
            return OrganizationUseCaseErrors.OnlyAdminsCanRemoveOthers;
        }

        var removed = organization.RemoveMember(command.UserId);
        if (removed.IsFailure)
        {
            return removed;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
