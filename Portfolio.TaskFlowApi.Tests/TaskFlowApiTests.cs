using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Portfolio.TaskFlowApi.Api.Contracts.Auth;
using Portfolio.TaskFlowApi.Api.Contracts.Projects;
using Portfolio.TaskFlowApi.Api.Contracts.Tasks;
using Portfolio.TaskFlowApi.Core.Domain;
using DomainTaskStatus = Portfolio.TaskFlowApi.Core.Domain.TaskStatus;

namespace Portfolio.TaskFlowApi.Tests;

public sealed class TaskFlowApiTests : IClassFixture<ApiWebApplicationFactory>, IAsyncLifetime, IDisposable
{
    private const string TestOnlyPassword = "test-only-password-123";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly ApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TaskFlowApiTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();

        var email = $"stage1-regression-{Guid.NewGuid():N}@example.test";
        var registerResponse = await PostJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = TestOnlyPassword,
        });
        registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await PostJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = TestOnlyPassword,
        });
        loginResponse.EnsureSuccessStatusCode();
        var auth = await ReadAsync<AuthResponse>(loginResponse);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task CreateProject_ReturnsCreatedProjectAndLocation()
    {
        var response = await PostJsonAsync("/api/projects", new CreateProjectRequest
        {
            Name = "  Portfolio API  ",
            Description = "  Stage 1  ",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var project = await ReadAsync<ProjectResponse>(response);
        Assert.Equal("Portfolio API", project.Name);
        Assert.Equal("Stage 1", project.Description);
        Assert.NotEqual(Guid.Empty, project.Id);
        Assert.Equal($"/api/projects/{project.Id}", response.Headers.Location?.PathAndQuery);
    }

    [Fact]
    public async Task CreateProject_WithoutName_ReturnsBadRequest()
    {
        var response = await PostRawJsonAsync("/api/projects", "{\"description\":\"No name\"}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CreateProject_WithWhitespaceName_ReturnsBadRequest()
    {
        var response = await PostJsonAsync("/api/projects", new CreateProjectRequest { Name = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateProject_WithTooLongName_ReturnsBadRequest()
    {
        var response = await PostJsonAsync("/api/projects", new CreateProjectRequest
        {
            Name = new string('n', DomainRules.ProjectNameMaxLength + 1),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateProject_WithTooLongDescription_ReturnsBadRequest()
    {
        var response = await PostJsonAsync("/api/projects", new CreateProjectRequest
        {
            Name = "Valid",
            Description = new string('d', DomainRules.DescriptionMaxLength + 1),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListProjects_ReturnsCreatedProjects()
    {
        await CreateProjectAsync("First");
        await CreateProjectAsync("Second");

        var projects = await _client.GetFromJsonAsync<List<ProjectResponse>>("/api/projects", JsonOptions);

        Assert.NotNull(projects);
        Assert.Equal(2, projects.Count);
        Assert.Contains(projects, project => project.Name == "First");
        Assert.Contains(projects, project => project.Name == "Second");
    }

    [Fact]
    public async Task GetProject_ReturnsExistingProject()
    {
        var created = await CreateProjectAsync("Retrieve me");

        var response = await _client.GetAsync($"/api/projects/{created.Id}");
        var project = await ReadAsync<ProjectResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created, project);
    }

    [Fact]
    public async Task GetProject_WithMalformedId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/projects/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProject_WhenMissing_ReturnsProblemDetailsNotFound()
    {
        var response = await _client.GetAsync($"/api/projects/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UpdateProject_ChangesPersistedDetails()
    {
        var created = await CreateProjectAsync("Before");

        var updateResponse = await PutJsonAsync($"/api/projects/{created.Id}", new UpdateProjectRequest
        {
            Name = "  After  ",
            Description = "  Updated  ",
        });
        var updated = await _client.GetFromJsonAsync<ProjectResponse>($"/api/projects/{created.Id}", JsonOptions);

        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal("After", updated.Name);
        Assert.Equal("Updated", updated.Description);
    }

    [Fact]
    public async Task UpdateProject_WhenMissing_ReturnsNotFound()
    {
        var response = await PutJsonAsync($"/api/projects/{Guid.NewGuid()}", new UpdateProjectRequest { Name = "Valid" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProject_WithInvalidName_ReturnsBadRequestAndDoesNotChangeProject()
    {
        var created = await CreateProjectAsync("Original");

        var response = await PutJsonAsync($"/api/projects/{created.Id}", new UpdateProjectRequest { Name = " " });
        var unchanged = await _client.GetFromJsonAsync<ProjectResponse>($"/api/projects/{created.Id}", JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Original", unchanged?.Name);
    }

    [Fact]
    public async Task DeleteProject_RemovesProject()
    {
        var created = await CreateProjectAsync("Delete me");

        var deleteResponse = await _client.DeleteAsync($"/api/projects/{created.Id}");
        var getResponse = await _client.GetAsync($"/api/projects/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteProject_WhenMissing_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync($"/api/projects/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateTask_ReturnsTodoTaskAndLocation()
    {
        var project = await CreateProjectAsync("Task owner");

        var response = await PostJsonAsync($"/api/projects/{project.Id}/tasks", new CreateTaskRequest
        {
            Title = "  Write tests  ",
            Description = "  Integration tests  ",
            DueDateUtc = DateTime.UtcNow.AddDays(2),
        });
        var task = await ReadAsync<TaskResponse>(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(project.Id, task.ProjectId);
        Assert.Equal("Write tests", task.Title);
        Assert.Equal("Integration tests", task.Description);
        Assert.Equal(DomainTaskStatus.Todo, task.Status);
        Assert.Equal($"/api/tasks/{task.Id}", response.Headers.Location?.PathAndQuery);
    }

    [Fact]
    public async Task CreateTask_WhenProjectMissing_ReturnsNotFound()
    {
        var response = await PostJsonAsync($"/api/projects/{Guid.NewGuid()}/tasks", new CreateTaskRequest { Title = "Task" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateTask_WithoutTitle_ReturnsBadRequest()
    {
        var project = await CreateProjectAsync("Owner");

        var response = await PostRawJsonAsync($"/api/projects/{project.Id}/tasks", "{}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTask_WithTooLongTitle_ReturnsBadRequest()
    {
        var project = await CreateProjectAsync("Owner");

        var response = await PostJsonAsync($"/api/projects/{project.Id}/tasks", new CreateTaskRequest
        {
            Title = new string('t', DomainRules.TaskTitleMaxLength + 1),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTask_WithPastDueDate_ReturnsBadRequest()
    {
        var project = await CreateProjectAsync("Owner");

        var response = await PostJsonAsync($"/api/projects/{project.Id}/tasks", new CreateTaskRequest
        {
            Title = "Already late",
            DueDateUtc = DateTime.UtcNow.AddDays(-1),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTask_ReturnsExistingTask()
    {
        var project = await CreateProjectAsync("Owner");
        var created = await CreateTaskAsync(project.Id, "Retrieve me");

        var response = await _client.GetAsync($"/api/tasks/{created.Id}");
        var task = await ReadAsync<TaskResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created, task);
    }

    [Fact]
    public async Task GetTask_WhenMissing_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/tasks/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListProjectTasks_ReturnsOnlyTasksForRequestedProject()
    {
        var firstProject = await CreateProjectAsync("First");
        var secondProject = await CreateProjectAsync("Second");
        await CreateTaskAsync(firstProject.Id, "First A");
        await CreateTaskAsync(firstProject.Id, "First B");
        await CreateTaskAsync(secondProject.Id, "Second A");

        var tasks = await _client.GetFromJsonAsync<List<TaskResponse>>(
            $"/api/projects/{firstProject.Id}/tasks",
            JsonOptions);

        Assert.NotNull(tasks);
        Assert.Equal(2, tasks.Count);
        Assert.All(tasks, task => Assert.Equal(firstProject.Id, task.ProjectId));
    }

    [Fact]
    public async Task ListProjectTasks_WhenProjectMissing_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/projects/{Guid.NewGuid()}/tasks");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTask_ChangesFieldsAndRetainsStatus()
    {
        var project = await CreateProjectAsync("Owner");
        var created = await CreateTaskAsync(project.Id, "Before");
        var dueDate = DateTime.UtcNow.AddDays(4);

        var updateResponse = await PutJsonAsync($"/api/tasks/{created.Id}", new UpdateTaskRequest
        {
            Title = "After",
            Description = "Updated",
            DueDateUtc = dueDate,
        });
        var updated = await _client.GetFromJsonAsync<TaskResponse>($"/api/tasks/{created.Id}", JsonOptions);

        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal("After", updated.Title);
        Assert.Equal("Updated", updated.Description);
        Assert.Equal(dueDate, updated.DueDateUtc);
        Assert.Equal(DomainTaskStatus.Todo, updated.Status);
    }

    [Fact]
    public async Task UpdateTask_WhenMissing_ReturnsNotFound()
    {
        var response = await PutJsonAsync($"/api/tasks/{Guid.NewGuid()}", new UpdateTaskRequest { Title = "Valid" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTask_WithWhitespaceTitle_ReturnsBadRequest()
    {
        var project = await CreateProjectAsync("Owner");
        var task = await CreateTaskAsync(project.Id, "Original");

        var response = await PutJsonAsync($"/api/tasks/{task.Id}", new UpdateTaskRequest { Title = "  " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTask_RemovesTaskButKeepsProject()
    {
        var project = await CreateProjectAsync("Owner");
        var task = await CreateTaskAsync(project.Id, "Delete me");

        var deleteResponse = await _client.DeleteAsync($"/api/tasks/{task.Id}");
        var taskResponse = await _client.GetAsync($"/api/tasks/{task.Id}");
        var projectResponse = await _client.GetAsync($"/api/projects/{project.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, taskResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, projectResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteTask_WhenMissing_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync($"/api/tasks/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangeTaskStatus_ThroughValidWorkflow_PersistsEachChange()
    {
        var project = await CreateProjectAsync("Owner");
        var task = await CreateTaskAsync(project.Id, "Workflow");

        var inProgressResponse = await PatchStatusAsync(task.Id, DomainTaskStatus.InProgress);
        var inProgressTask = await ReadAsync<TaskResponse>(inProgressResponse);
        var completedResponse = await PatchStatusAsync(task.Id, DomainTaskStatus.Completed);
        var completedTask = await ReadAsync<TaskResponse>(completedResponse);

        Assert.Equal(HttpStatusCode.OK, inProgressResponse.StatusCode);
        Assert.Equal(DomainTaskStatus.InProgress, inProgressTask.Status);
        Assert.Equal(HttpStatusCode.OK, completedResponse.StatusCode);
        Assert.Equal(DomainTaskStatus.Completed, completedTask.Status);
    }

    [Fact]
    public async Task ChangeTaskStatus_WhenTransitionSkipsStep_ReturnsBadRequest()
    {
        var project = await CreateProjectAsync("Owner");
        var task = await CreateTaskAsync(project.Id, "Workflow");

        var response = await PatchStatusAsync(task.Id, DomainTaskStatus.Completed);
        var unchanged = await _client.GetFromJsonAsync<TaskResponse>($"/api/tasks/{task.Id}", JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(DomainTaskStatus.Todo, unchanged?.Status);
    }

    [Fact]
    public async Task ChangeTaskStatus_WithUnknownStatus_ReturnsBadRequest()
    {
        var project = await CreateProjectAsync("Owner");
        var task = await CreateTaskAsync(project.Id, "Workflow");

        var response = await PostRawJsonAsPatchAsync($"/api/tasks/{task.Id}/status", "{\"status\":\"Unknown\"}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeTaskStatus_WithoutStatus_ReturnsBadRequest()
    {
        var project = await CreateProjectAsync("Owner");
        var task = await CreateTaskAsync(project.Id, "Workflow");

        var response = await PostRawJsonAsPatchAsync($"/api/tasks/{task.Id}/status", "{}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangeTaskStatus_WhenTaskMissing_ReturnsNotFound()
    {
        var response = await PatchStatusAsync(Guid.NewGuid(), DomainTaskStatus.InProgress);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProject_CascadesToItsTasks()
    {
        var project = await CreateProjectAsync("Cascade owner");
        var firstTask = await CreateTaskAsync(project.Id, "First");
        var secondTask = await CreateTaskAsync(project.Id, "Second");

        var deleteResponse = await _client.DeleteAsync($"/api/projects/{project.Id}");
        var firstResponse = await _client.GetAsync($"/api/tasks/{firstTask.Id}");
        var secondResponse = await _client.GetAsync($"/api/tasks/{secondTask.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, secondResponse.StatusCode);
    }

    [Fact]
    public async Task SwaggerDocument_IsAvailableInDevelopment()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("TaskFlow API", content, StringComparison.Ordinal);
        Assert.Contains("/api/projects", content, StringComparison.Ordinal);
    }

    private async Task<ProjectResponse> CreateProjectAsync(string name)
    {
        var response = await PostJsonAsync("/api/projects", new CreateProjectRequest { Name = name });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<ProjectResponse>(response);
    }

    private async Task<TaskResponse> CreateTaskAsync(Guid projectId, string title)
    {
        var response = await PostJsonAsync($"/api/projects/{projectId}/tasks", new CreateTaskRequest { Title = title });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<TaskResponse>(response);
    }

    private Task<HttpResponseMessage> PostJsonAsync<T>(string uri, T value) =>
        _client.PostAsJsonAsync(uri, value, JsonOptions);

    private Task<HttpResponseMessage> PutJsonAsync<T>(string uri, T value) =>
        _client.PutAsJsonAsync(uri, value, JsonOptions);

    private Task<HttpResponseMessage> PatchStatusAsync(Guid taskId, DomainTaskStatus status) =>
        _client.PatchAsJsonAsync($"/api/tasks/{taskId}/status", new UpdateTaskStatusRequest { Status = status }, JsonOptions);

    private Task<HttpResponseMessage> PostRawJsonAsync(string uri, string json) =>
        _client.PostAsync(uri, new StringContent(json, Encoding.UTF8, "application/json"));

    private Task<HttpResponseMessage> PostRawJsonAsPatchAsync(string uri, string json) =>
        _client.PatchAsync(uri, new StringContent(json, Encoding.UTF8, "application/json"));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(JsonOptions)
        ?? throw new InvalidOperationException($"Response did not contain a {typeof(T).Name} body.");

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return options;
    }
}
