using Microsoft.EntityFrameworkCore;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Domain.Labels;

namespace ProjectFlow.Infrastructure.Persistence.Repositories;

internal sealed class LabelRepository(ApplicationDbContext dbContext) : ILabelRepository
{
    public Task<Label?> GetAsync(Guid projectId, Guid labelId, CancellationToken cancellationToken) =>
        dbContext.Labels.SingleOrDefaultAsync(label => label.Id == labelId && label.ProjectId == projectId, cancellationToken);

    // PostgreSQL lower() is Unicode-aware, so "Diseño" and "DISEÑO" are the same name.
    public Task<bool> ExistsWithNameAsync(Guid projectId, string name, Guid? exceptLabelId, CancellationToken cancellationToken) =>
        dbContext.Labels.AnyAsync(
            label => label.ProjectId == projectId
                && label.Name.ToLower() == name.ToLower()
                && label.Id != exceptLabelId,
            cancellationToken);

    public void Add(Label label) => dbContext.Labels.Add(label);

    public void Remove(Label label) => dbContext.Labels.Remove(label);
}
