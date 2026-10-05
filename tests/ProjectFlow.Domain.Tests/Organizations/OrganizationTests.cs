using ProjectFlow.Domain.Organizations;

namespace ProjectFlow.Domain.Tests.Organizations;

public class OrganizationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly Slug ValidSlug = Slug.Create("acme").Value;
    private static readonly Guid Creator = Guid.NewGuid();

    private static Organization CreateOrganization() =>
        Organization.Create("Acme", ValidSlug, Creator, Now).Value;

    [Fact]
    public void Create_makes_the_creator_the_first_admin()
    {
        var result = Organization.Create("  Acme Software ", ValidSlug, Creator, Now);

        Assert.True(result.IsSuccess);
        var organization = result.Value;
        Assert.Equal("Acme Software", organization.Name);
        Assert.Equal(ValidSlug, organization.Slug);
        var member = Assert.Single(organization.Members);
        Assert.Equal(Creator, member.UserId);
        Assert.Equal(organization.Id, member.OrganizationId);
        Assert.Equal(OrganizationRole.Admin, member.Role);
        Assert.True(organization.IsAdmin(Creator));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_requires_a_name(string name)
    {
        Assert.Equal(OrganizationErrors.NameRequired, Organization.Create(name, ValidSlug, Creator, Now).Error);
    }

    [Fact]
    public void Create_rejects_a_too_long_name()
    {
        var name = new string('a', Organization.NameMaxLength + 1);

        Assert.Equal(OrganizationErrors.NameTooLong, Organization.Create(name, ValidSlug, Creator, Now).Error);
    }

    [Fact]
    public void Create_requires_a_creator()
    {
        Assert.Equal(OrganizationErrors.UserRequired, Organization.Create("Acme", ValidSlug, Guid.Empty, Now).Error);
    }

    [Fact]
    public void AddMember_adds_a_new_member()
    {
        var organization = CreateOrganization();
        var userId = Guid.NewGuid();

        var result = organization.AddMember(userId, OrganizationRole.Member, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, organization.Members.Count);
        Assert.False(organization.IsAdmin(userId));
    }

    [Fact]
    public void AddMember_rejects_an_existing_member()
    {
        var organization = CreateOrganization();

        Assert.Equal(OrganizationErrors.MemberAlreadyExists, organization.AddMember(Creator, OrganizationRole.Member, Now).Error);
    }

    [Fact]
    public void AddMember_rejects_an_undefined_role()
    {
        var organization = CreateOrganization();

        Assert.Equal(OrganizationErrors.RoleInvalid, organization.AddMember(Guid.NewGuid(), (OrganizationRole)99, Now).Error);
    }

    [Fact]
    public void ChangeMemberRole_cannot_demote_the_last_admin()
    {
        var organization = CreateOrganization();

        var result = organization.ChangeMemberRole(Creator, OrganizationRole.Member);

        Assert.Equal(OrganizationErrors.LastAdmin, result.Error);
        Assert.True(organization.IsAdmin(Creator));
    }

    [Fact]
    public void ChangeMemberRole_can_demote_an_admin_when_another_admin_exists()
    {
        var organization = CreateOrganization();
        var secondAdmin = Guid.NewGuid();
        organization.AddMember(secondAdmin, OrganizationRole.Admin, Now);

        var result = organization.ChangeMemberRole(Creator, OrganizationRole.Member);

        Assert.True(result.IsSuccess);
        Assert.False(organization.IsAdmin(Creator));
        Assert.True(organization.IsAdmin(secondAdmin));
    }

    [Fact]
    public void ChangeMemberRole_requires_an_existing_member()
    {
        var organization = CreateOrganization();

        Assert.Equal(OrganizationErrors.MemberNotFound, organization.ChangeMemberRole(Guid.NewGuid(), OrganizationRole.Admin).Error);
    }

    [Fact]
    public void RemoveMember_cannot_remove_the_last_admin()
    {
        var organization = CreateOrganization();
        organization.AddMember(Guid.NewGuid(), OrganizationRole.Member, Now);

        Assert.Equal(OrganizationErrors.LastAdmin, organization.RemoveMember(Creator).Error);
        Assert.Equal(2, organization.Members.Count);
    }

    [Fact]
    public void RemoveMember_removes_a_regular_member()
    {
        var organization = CreateOrganization();
        var userId = Guid.NewGuid();
        organization.AddMember(userId, OrganizationRole.Member, Now);

        var result = organization.RemoveMember(userId);

        Assert.True(result.IsSuccess);
        Assert.Single(organization.Members);
    }

    [Fact]
    public void RemoveMember_requires_an_existing_member()
    {
        var organization = CreateOrganization();

        Assert.Equal(OrganizationErrors.MemberNotFound, organization.RemoveMember(Guid.NewGuid()).Error);
    }

    [Fact]
    public void Rename_validates_and_trims()
    {
        var organization = CreateOrganization();

        Assert.Equal(OrganizationErrors.NameRequired, organization.Rename(" ").Error);
        Assert.True(organization.Rename(" Acme Labs ").IsSuccess);
        Assert.Equal("Acme Labs", organization.Name);
    }
}
