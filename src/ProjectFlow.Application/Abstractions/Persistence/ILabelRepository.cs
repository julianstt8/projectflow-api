using ProjectFlow.Domain.Labels;

namespace ProjectFlow.Application.Abstractions.Persistence;

public interface ILabelRepository
{
    /// <summary>The label, only if it belongs to <paramref name="projectId"/>.</summary>
    Task<Label?> GetAsync(Guid projectId, Guid labelId, CancellationToken cancellationToken);

    /// <summary>Case-insensitive; <paramref name="exceptLabelId"/> lets a label keep its own name.</summary>
    Task<bool> ExistsWithNameAsync(Guid projectId, string name, Guid? exceptLabelId, CancellationToken cancellationToken);

    void Add(Label label);

    /// <summary>Deletes the label for good; it is removed from every task.</summary>
    void Remove(Label label);
}
