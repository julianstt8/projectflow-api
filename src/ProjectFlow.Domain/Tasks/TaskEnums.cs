namespace ProjectFlow.Domain.Tasks;

public enum TaskType
{
    Story = 1,
    Bug = 2,
    Task = 3,
}

public enum TaskPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
}

public enum TaskItemStatus
{
    ToDo = 1,
    InProgress = 2,
    Review = 3,
    Done = 4,
}
