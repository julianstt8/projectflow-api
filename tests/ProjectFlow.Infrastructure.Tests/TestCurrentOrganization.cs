using ProjectFlow.Application.Abstractions;

namespace ProjectFlow.Infrastructure.Tests;

internal sealed class TestCurrentOrganization(Guid? organizationId) : ICurrentOrganization
{
    public Guid? OrganizationId { get; } = organizationId;
}
