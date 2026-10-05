namespace ProjectFlow.Domain.Tasks;

public sealed class TaskLabel
{
    internal TaskLabel(Guid taskId, Guid labelId)
    {
        TaskId = taskId;
        LabelId = labelId;
    }

    public Guid TaskId { get; }

    public Guid LabelId { get; }
}
