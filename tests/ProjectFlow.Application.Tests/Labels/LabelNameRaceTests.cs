using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Application.Labels;
using ProjectFlow.Application.Tests.Authentication;
using ProjectFlow.Domain.Labels;
using ProjectFlow.Domain.Projects;

namespace ProjectFlow.Application.Tests.Labels;

/// <summary>
/// Two requests with the same label name can both pass the name check; the unique index stops the second one
/// and the use case must answer the same error as the check (not a generic duplicate).
/// </summary>
public class LabelNameRaceTests
{
    private const string NameIndex = "ix_labels_project_id_lower_name";
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly Project _project = Project.Create(Guid.NewGuid(), ProjectKey.Create("WEB").Value, "Web", null, Guid.NewGuid(), Now).Value;
    private readonly InMemoryLabelRepository _labels = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly SingleProjectRepository _projects;

    public LabelNameRaceTests()
    {
        _projects = new SingleProjectRepository(_project);
    }

    [Fact]
    public async Task Creating_a_label_whose_name_was_just_taken_answers_name_taken()
    {
        _unitOfWork.FailNextSaveWithDuplicateOn = NameIndex;
        var handler = new CreateLabelCommandHandler(_projects, _labels, _unitOfWork, new FixedTimeProvider(Now));

        var result = await handler.Handle(new CreateLabelCommand(_project.Id, "backend", "#1D76DB"), CancellationToken.None);

        Assert.Equal(LabelUseCaseErrors.NameTaken, result.Error);
    }

    [Fact]
    public async Task Renaming_a_label_to_a_name_just_taken_answers_name_taken()
    {
        var label = Label.Create(_project, "frontend", "#1D76DB", Now).Value;
        _labels.Add(label);
        _unitOfWork.FailNextSaveWithDuplicateOn = NameIndex;
        var handler = new UpdateLabelCommandHandler(_projects, _labels, _unitOfWork);

        var result = await handler.Handle(new UpdateLabelCommand(_project.Id, label.Id, "backend", "#1D76DB"), CancellationToken.None);

        Assert.Equal(LabelUseCaseErrors.NameTaken, result.Error);
    }

    private sealed class SingleProjectRepository(Project project) : IProjectRepository
    {
        public Task<bool> ExistsWithKeyAsync(Guid organizationId, ProjectKey key, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(id == project.Id ? project : null);

        public Task LockForTaskNumberingAsync(Guid projectId, CancellationToken cancellationToken) => Task.CompletedTask;

        public void Add(Project added)
        {
        }
    }

    /// <summary>Finds no other label with the name, as when both requests check before either saves.</summary>
    private sealed class InMemoryLabelRepository : ILabelRepository
    {
        private readonly List<Label> _labels = [];

        public Task<Label?> GetAsync(Guid projectId, Guid labelId, CancellationToken cancellationToken) =>
            Task.FromResult(_labels.Find(label => label.ProjectId == projectId && label.Id == labelId));

        public Task<bool> ExistsWithNameAsync(Guid projectId, string name, Guid? exceptLabelId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public void Add(Label label) => _labels.Add(label);

        public void Remove(Label label) => _labels.Remove(label);
    }
}
