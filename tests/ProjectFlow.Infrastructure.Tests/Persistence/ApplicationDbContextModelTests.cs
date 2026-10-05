using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ProjectFlow.Domain.Common;
using ProjectFlow.Domain.Sprints;
using ProjectFlow.Domain.Tasks;
using ProjectFlow.Infrastructure.Persistence;

namespace ProjectFlow.Infrastructure.Tests.Persistence;

/// <summary>Model checks that need no database. Behavior against PostgreSQL is covered with Testcontainers (#7).</summary>
public sealed class ApplicationDbContextModelTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext = new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=projectflow_model_tests")
            .UseSnakeCaseNamingConvention()
            .Options,
        new TestCurrentOrganization(null));

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public void Migrations_are_up_to_date_with_the_model()
    {
        Assert.False(
            _dbContext.Database.HasPendingModelChanges(),
            "The model has changes that are not in a migration. Run `dotnet ef migrations add <Name>`.");
    }

    [Fact]
    public void Every_entity_can_be_materialized_through_a_constructor()
    {
        var entityTypes = _dbContext.Model.GetEntityTypes();

        Assert.All(entityTypes, entityType =>
            Assert.True(
                entityType.ConstructorBinding is not null,
                $"{entityType.ClrType.Name} has no constructor EF Core can bind to."));
    }

    [Theory]
    [InlineData(typeof(IOrganizationOwned), ApplicationDbContext.OrganizationFilter)]
    [InlineData(typeof(ISoftDeletable), ApplicationDbContext.SoftDeleteFilter)]
    public void Entities_implementing_a_marker_interface_have_its_query_filter(Type markerInterface, string filterKey)
    {
        var entityTypes = _dbContext.Model.GetEntityTypes()
            .Where(entityType => markerInterface.IsAssignableFrom(entityType.ClrType))
            .ToList();

        Assert.NotEmpty(entityTypes);
        Assert.All(entityTypes, entityType =>
            Assert.Contains(entityType.GetDeclaredQueryFilters(), filter => filter.Key == filterKey));
    }

    [Fact]
    public void Only_one_active_sprint_per_project_is_enforced_by_a_partial_unique_index()
    {
        var index = Assert.Single(
            EntityType<Sprint>().GetIndexes(),
            index => index.GetDatabaseName() == "ix_sprints_project_id_active");

        Assert.True(index.IsUnique);
        Assert.Equal("status = 'Active'", index.GetFilter());
    }

    [Fact]
    public void Tasks_reference_their_project_with_a_composite_key_that_includes_the_organization()
    {
        var foreignKey = Assert.Single(
            EntityType<TaskItem>().GetForeignKeys(),
            foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(ProjectFlow.Domain.Projects.Project));

        Assert.Equal(["project_id", "organization_id"], foreignKey.Properties.Select(property => property.GetColumnName()));
    }

    [Fact]
    public void Tables_and_columns_use_snake_case()
    {
        Assert.Equal("tasks", EntityType<TaskItem>().GetTableName());
        Assert.Equal("story_points", EntityType<TaskItem>().FindProperty(nameof(TaskItem.StoryPoints))!.GetColumnName());
    }

    private IEntityType EntityType<TEntity>() => _dbContext.Model.FindEntityType(typeof(TEntity))!;
}
