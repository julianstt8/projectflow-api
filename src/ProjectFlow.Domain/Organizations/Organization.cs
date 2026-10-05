using ProjectFlow.Domain.Common;

namespace ProjectFlow.Domain.Organizations;

public sealed class Organization : Entity
{
    public const int NameMaxLength = 100;

    private readonly List<OrganizationMember> _members = [];

    private Organization(Guid id, string name, Slug slug, DateTimeOffset createdAt)
        : base(id)
    {
        Name = name;
        Slug = slug;
        CreatedAt = createdAt;
    }

    public string Name { get; private set; }

    public Slug Slug { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public IReadOnlyCollection<OrganizationMember> Members => _members.AsReadOnly();

    /// <summary>Creates an organization. The creator becomes its first admin.</summary>
    public static Result<Organization> Create(string name, Slug slug, Guid creatorUserId, DateTimeOffset now)
    {
        var normalizedName = NormalizeName(name);
        if (normalizedName.IsFailure)
        {
            return normalizedName.Error;
        }

        if (creatorUserId == Guid.Empty)
        {
            return OrganizationErrors.UserRequired;
        }

        var organization = new Organization(Guid.CreateVersion7(now), normalizedName.Value, slug, now);
        organization._members.Add(new OrganizationMember(organization.Id, creatorUserId, OrganizationRole.Admin, now));

        return organization;
    }

    public Result Rename(string name)
    {
        var normalizedName = NormalizeName(name);
        if (normalizedName.IsFailure)
        {
            return normalizedName.Error;
        }

        Name = normalizedName.Value;
        return Result.Success();
    }

    public bool IsAdmin(Guid userId) => FindMember(userId)?.Role == OrganizationRole.Admin;

    public Result AddMember(Guid userId, OrganizationRole role, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            return OrganizationErrors.UserRequired;
        }

        if (!Enum.IsDefined(role))
        {
            return OrganizationErrors.RoleInvalid;
        }

        if (FindMember(userId) is not null)
        {
            return OrganizationErrors.MemberAlreadyExists;
        }

        _members.Add(new OrganizationMember(Id, userId, role, now));
        return Result.Success();
    }

    public Result ChangeMemberRole(Guid userId, OrganizationRole role)
    {
        if (!Enum.IsDefined(role))
        {
            return OrganizationErrors.RoleInvalid;
        }

        var member = FindMember(userId);
        if (member is null)
        {
            return OrganizationErrors.MemberNotFound;
        }

        if (role != OrganizationRole.Admin && IsLastAdmin(member))
        {
            return OrganizationErrors.LastAdmin;
        }

        member.ChangeRole(role);
        return Result.Success();
    }

    public Result RemoveMember(Guid userId)
    {
        var member = FindMember(userId);
        if (member is null)
        {
            return OrganizationErrors.MemberNotFound;
        }

        if (IsLastAdmin(member))
        {
            return OrganizationErrors.LastAdmin;
        }

        _members.Remove(member);
        return Result.Success();
    }

    private OrganizationMember? FindMember(Guid userId) => _members.Find(member => member.UserId == userId);

    private bool IsLastAdmin(OrganizationMember member) =>
        member.Role == OrganizationRole.Admin && _members.Count(m => m.Role == OrganizationRole.Admin) == 1;

    private static Result<string> NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return OrganizationErrors.NameRequired;
        }

        var trimmed = name.Trim();
        if (trimmed.Length > NameMaxLength)
        {
            return OrganizationErrors.NameTooLong;
        }

        return trimmed;
    }
}
