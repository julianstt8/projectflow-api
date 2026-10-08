using System.Net;
using ProjectFlow.Api.Controllers;
using ProjectFlow.Api.IntegrationTests.Infrastructure;
using ProjectFlow.Application.Comments;
using ProjectFlow.Application.Labels;
using ProjectFlow.Application.Tasks;
using ProjectFlow.Domain.Comments;
using ProjectFlow.Domain.Projects;
using ProjectFlow.Domain.Tasks;

namespace ProjectFlow.Api.IntegrationTests.Tasks;

/// <summary>RF-09: comments (any language, author-only edit) and project labels on tasks.</summary>
[Collection(ApiCollection.Name)]
public class CommentAndLabelEndpointsTests(ProjectFlowApiFactory api)
{
    private const string SpanishComment = "¿Revisaste la validación del código? ¡Añadí pruebas para el señor Muñoz! Ñandú, acción, pingüino 🚀✅";

    // ---------- Comments ----------

    [Fact]
    public async Task Comments_keep_spanish_text_and_emoji_exactly_as_written()
    {
        var world = await WorldAsync();
        var task = await CreateTaskAsync(world, world.Developer);

        var response = await world.Developer.Client.PostJsonAsync(Comments(world, task), new CommentRequest(SpanishComment));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<CommentResponse>();
        var listed = Assert.Single(await (await world.Viewer.Client.GetAsync(Comments(world, task))).ReadAsync<List<CommentResponse>>());
        Assert.Equal(SpanishComment, created.Body);
        Assert.Equal(SpanishComment, listed.Body);
        Assert.Equal(world.Developer.Id, listed.AuthorId);
        Assert.Equal("developer", listed.AuthorName);
    }

    [Fact]
    public async Task The_length_limit_counts_characters_not_bytes()
    {
        var world = await WorldAsync();
        var task = await CreateTaskAsync(world, world.Developer);

        // "ñ" takes two bytes in UTF-8: a byte-based limit would reject this valid comment.
        var atLimit = await world.Developer.Client.PostJsonAsync(Comments(world, task), new CommentRequest(new string('ñ', Comment.BodyMaxLength)));
        var overLimit = await world.Developer.Client.PostJsonAsync(Comments(world, task), new CommentRequest(new string('ñ', Comment.BodyMaxLength + 1)));

        Assert.Equal(HttpStatusCode.Created, atLimit.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, overLimit.StatusCode);
    }

    [Fact]
    public async Task Viewers_read_comments_but_cannot_write_them()
    {
        var world = await WorldAsync();
        var task = await CreateTaskAsync(world, world.Developer);

        var response = await world.Viewer.Client.PostJsonAsync(Comments(world, task), new CommentRequest("Hola"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await world.Viewer.Client.GetAsync(Comments(world, task))).StatusCode);
    }

    [Fact]
    public async Task Only_the_author_edits_a_comment()
    {
        var world = await WorldAsync();
        var task = await CreateTaskAsync(world, world.Developer);
        var comment = await CommentAsync(world, world.Developer, task, "Primera versión");

        var byOther = await world.OtherDeveloper.Client.PutJsonAsync($"{Comments(world, task)}/{comment}", new CommentRequest("Hackeado"));
        var byManager = await world.Org.Admin.Client.PutJsonAsync($"{Comments(world, task)}/{comment}", new CommentRequest("Editado por el PM"));
        var byAuthor = await world.Developer.Client.PutJsonAsync($"{Comments(world, task)}/{comment}", new CommentRequest("Versión corregida ✍️"));

        Assert.Equal(HttpStatusCode.Forbidden, byOther.StatusCode);
        Assert.Equal("Comment.NotAuthor", await byOther.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.Forbidden, byManager.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, byAuthor.StatusCode);
        var listed = Assert.Single(await (await world.Developer.Client.GetAsync(Comments(world, task))).ReadAsync<List<CommentResponse>>());
        Assert.Equal("Versión corregida ✍️", listed.Body);
    }

    [Fact]
    public async Task Authors_delete_their_comments_and_project_managers_moderate()
    {
        var world = await WorldAsync();
        var task = await CreateTaskAsync(world, world.Developer);
        var mine = await CommentAsync(world, world.Developer, task, "Mío");
        var other = await CommentAsync(world, world.OtherDeveloper, task, "Del otro");
        var spam = await CommentAsync(world, world.OtherDeveloper, task, "Spam");

        var deleteOthers = await world.Developer.Client.DeleteAsync($"{Comments(world, task)}/{other}");
        var deleteMine = await world.Developer.Client.DeleteAsync($"{Comments(world, task)}/{mine}");
        var moderate = await world.Org.Admin.Client.DeleteAsync($"{Comments(world, task)}/{spam}");

        Assert.Equal(HttpStatusCode.Forbidden, deleteOthers.StatusCode);
        Assert.Equal("Comment.CannotDelete", await deleteOthers.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.NoContent, deleteMine.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, moderate.StatusCode);
        var remaining = await (await world.Developer.Client.GetAsync(Comments(world, task))).ReadAsync<List<CommentResponse>>();
        Assert.Equal([other], remaining.Select(comment => comment.Id));
    }

    [Fact]
    public async Task Done_tasks_can_be_commented_but_archived_projects_cannot()
    {
        var world = await WorldAsync();
        var done = await api.AddTaskAsync(world.Org.Id, world.ProjectId, status: TaskItemStatus.Done);

        var onDone = await world.Org.Admin.Client.PostJsonAsync(Comments(world, done), new CommentRequest("Entregado 🎉"));
        await world.Org.Admin.Client.PostAsync($"{world.Project}/archive", null);
        var onArchived = await world.Org.Admin.Client.PostJsonAsync(Comments(world, done), new CommentRequest("Tarde"));

        Assert.Equal(HttpStatusCode.Created, onDone.StatusCode);
        Assert.Equal("Project.Archived", await onArchived.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Comments_of_a_task_cannot_be_reached_through_another_project()
    {
        var world = await WorldAsync();
        var task = await CreateTaskAsync(world, world.Developer);
        var otherProject = (await world.Org.CreateProjectAsync("OTH")).Id;

        var response = await world.Org.Admin.Client.GetAsync($"{world.Org.Project(otherProject)}/tasks/{task}/comments");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Task.NotFound", await response.ReadErrorCodeAsync());
    }

    // ---------- Labels ----------

    [Fact]
    public async Task Label_names_accept_spanish_and_are_unique_ignoring_case()
    {
        var world = await WorldAsync();

        var created = await world.Org.Admin.Client.PostJsonAsync(Labels(world), new LabelRequest("Diseño", "#d73a4a"));
        var duplicate = await world.Org.Admin.Client.PostJsonAsync(Labels(world), new LabelRequest("DISEÑO", "#000000"));
        var badColor = await world.Org.Admin.Client.PostJsonAsync(Labels(world), new LabelRequest("Rojo", "red"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var label = await created.ReadAsync<LabelResponse>();
        Assert.Equal(("Diseño", "#D73A4A"), (label.Name, label.Color));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("Label.NameTaken", await duplicate.ReadErrorCodeAsync());
        Assert.Equal("Label.ColorInvalid", await badColor.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Labels_created_at_the_same_time_with_the_same_name_keep_only_one()
    {
        var world = await WorldAsync();
        string[] names = ["Release", "release", "RELEASE", "ReLeAsE"];

        var responses = await Task.WhenAll(names.Select(name =>
            world.Org.Admin.Client.PostJsonAsync(Labels(world), new LabelRequest(name, "#1D76DB"))));

        // Whichever stops a duplicate (the name check or, in a race, the unique index), the answer is the same.
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        foreach (var rejected in responses.Where(response => response.StatusCode != HttpStatusCode.Created))
        {
            Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
            Assert.Equal("Label.NameTaken", await rejected.ReadErrorCodeAsync());
        }

        var labels = await (await world.Org.Admin.Client.GetAsync(Labels(world))).ReadAsync<List<LabelResponse>>();
        Assert.Single(labels, label => label.Name.Equals("release", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Only_project_managers_manage_labels()
    {
        var world = await WorldAsync();

        var byDeveloper = await world.Developer.Client.PostJsonAsync(Labels(world), new LabelRequest("backend", "#1D76DB"));

        Assert.Equal(HttpStatusCode.Forbidden, byDeveloper.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await world.Viewer.Client.GetAsync(Labels(world))).StatusCode);
    }

    [Fact]
    public async Task Developers_tag_their_own_tasks_with_labels_of_the_project()
    {
        var world = await WorldAsync();
        var label = await CreateLabelAsync(world, "frontend");
        var otherProject = (await world.Org.CreateProjectAsync("OTH")).Id;
        var foreignLabel = await CreateLabelAsync(world, "ajeno", otherProject);
        var mine = await CreateTaskAsync(world, world.Developer);
        var others = await CreateTaskAsync(world, world.OtherDeveloper);

        var tag = await world.Developer.Client.PutAsync($"{world.Tasks}/{mine}/labels/{label}", null);
        var tagAgain = await world.Developer.Client.PutAsync($"{world.Tasks}/{mine}/labels/{label}", null);
        var tagOthers = await world.Developer.Client.PutAsync($"{world.Tasks}/{others}/labels/{label}", null);
        var tagForeign = await world.Developer.Client.PutAsync($"{world.Tasks}/{mine}/labels/{foreignLabel}", null);

        Assert.Equal(HttpStatusCode.NoContent, tag.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, tagAgain.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, tagOthers.StatusCode);
        Assert.Equal("Label.NotInProject", await tagForeign.ReadErrorCodeAsync());
        Assert.Equal([label], (await GetTaskAsync(world, mine)).LabelIds);

        Assert.Equal(HttpStatusCode.NoContent, (await world.Developer.Client.DeleteAsync($"{world.Tasks}/{mine}/labels/{label}")).StatusCode);
        Assert.Empty((await GetTaskAsync(world, mine)).LabelIds);
    }

    [Fact]
    public async Task Deleting_a_label_removes_it_from_every_task()
    {
        var world = await WorldAsync();
        var label = await CreateLabelAsync(world, "temporal");
        var task = await CreateTaskAsync(world, world.Developer);
        await world.Developer.Client.PutAsync($"{world.Tasks}/{task}/labels/{label}", null);

        var response = await world.Org.Admin.Client.DeleteAsync($"{Labels(world)}/{label}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await GetTaskAsync(world, task)).LabelIds);
        Assert.DoesNotContain(await (await world.Org.Admin.Client.GetAsync(Labels(world))).ReadAsync<List<LabelResponse>>(), l => l.Id == label);
    }

    // ---------- Helpers ----------

    private sealed record World(TestOrganization Org, Guid ProjectId, ApiUser Developer, ApiUser OtherDeveloper, ApiUser Viewer)
    {
        public string Project => Org.Project(ProjectId);

        public string Tasks => $"{Project}/tasks";
    }

    private async Task<World> WorldAsync()
    {
        var org = await api.CreateOrganizationAsync();
        var project = (await org.CreateProjectAsync("CML")).Id;
        var developer = await org.AddProjectUserAsync(project, "developer", ProjectRole.Developer);
        var otherDeveloper = await org.AddProjectUserAsync(project, "other-developer", ProjectRole.Developer);
        var viewer = await org.AddProjectUserAsync(project, "viewer", ProjectRole.Viewer);
        return new World(org, project, developer, otherDeveloper, viewer);
    }

    private static string Comments(World world, Guid taskId) => $"{world.Tasks}/{taskId}/comments";

    private static string Labels(World world, Guid? projectId = null) => $"{world.Org.Project(projectId ?? world.ProjectId)}/labels";

    private static async Task<Guid> CreateTaskAsync(World world, ApiUser reporter)
    {
        var response = await reporter.Client.PostJsonAsync(
            world.Tasks,
            new CreateTaskRequest(TaskType.Task, "Tarea con comentarios", null, TaskPriority.Medium, null, null, null, null));
        response.EnsureSuccessStatusCode();
        return (await response.ReadAsync<TaskResponse>()).Id;
    }

    private static async Task<Guid> CommentAsync(World world, ApiUser author, Guid taskId, string body)
    {
        var response = await author.Client.PostJsonAsync(Comments(world, taskId), new CommentRequest(body));
        response.EnsureSuccessStatusCode();
        return (await response.ReadAsync<CommentResponse>()).Id;
    }

    private static async Task<Guid> CreateLabelAsync(World world, string name, Guid? projectId = null)
    {
        var response = await world.Org.Admin.Client.PostJsonAsync(Labels(world, projectId), new LabelRequest(name, "#1D76DB"));
        response.EnsureSuccessStatusCode();
        return (await response.ReadAsync<LabelResponse>()).Id;
    }

    private static async Task<TaskResponse> GetTaskAsync(World world, Guid taskId) =>
        await (await world.Org.Admin.Client.GetAsync($"{world.Tasks}/{taskId}")).ReadAsync<TaskResponse>();
}
