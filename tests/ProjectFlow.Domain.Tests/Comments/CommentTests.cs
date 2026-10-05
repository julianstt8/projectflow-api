using ProjectFlow.Domain.Comments;
using ProjectFlow.Domain.Tasks;
using static ProjectFlow.Domain.Tests.TestData;

namespace ProjectFlow.Domain.Tests.Comments;

public class CommentTests
{
    [Fact]
    public void Create_copies_task_and_organization()
    {
        var task = CreateTask(CreateProject());
        var author = Guid.NewGuid();

        var comment = Comment.Create(task, author, " Looks good ", Now).Value;

        Assert.Equal(task.Id, comment.TaskId);
        Assert.Equal(task.OrganizationId, comment.OrganizationId);
        Assert.Equal(author, comment.AuthorId);
        Assert.Equal("Looks good", comment.Body);
    }

    [Fact]
    public void Create_validates_input()
    {
        var task = CreateTask(CreateProject());

        Assert.Equal(CommentErrors.BodyRequired, Comment.Create(task, Guid.NewGuid(), " ", Now).Error);
        Assert.Equal(
            CommentErrors.BodyTooLong,
            Comment.Create(task, Guid.NewGuid(), new string('a', Comment.BodyMaxLength + 1), Now).Error);
        Assert.Equal(CommentErrors.AuthorRequired, Comment.Create(task, Guid.Empty, "Hi", Now).Error);
    }

    [Fact]
    public void Done_tasks_can_be_commented_but_deleted_ones_cannot()
    {
        var project = CreateProject();
        var done = CreateTaskIn(project, TaskItemStatus.Done);
        var deleted = CreateTask(project);
        deleted.Delete(Now);

        Assert.True(Comment.Create(done, Guid.NewGuid(), "Shipped", Now).IsSuccess);
        Assert.Equal(TaskErrors.Deleted, Comment.Create(deleted, Guid.NewGuid(), "Hi", Now).Error);
    }

    [Fact]
    public void Only_the_author_can_edit()
    {
        var author = Guid.NewGuid();
        var comment = Comment.Create(CreateTask(CreateProject()), author, "First", Now).Value;

        Assert.Equal(CommentErrors.NotAuthor, comment.Edit(Guid.NewGuid(), "Hacked").Error);
        Assert.True(comment.Edit(author, " Edited ").IsSuccess);
        Assert.Equal("Edited", comment.Body);
    }
}
