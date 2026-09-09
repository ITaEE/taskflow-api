using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.TaskFlowApi.Api.Contracts;
using Portfolio.TaskFlowApi.Api.Contracts.Tasks;
using Portfolio.TaskFlowApi.Core.Abstractions;
using Portfolio.TaskFlowApi.Core.Entities;

namespace Portfolio.TaskFlowApi.Api.Controllers;

[ApiController]
[Authorize]
public sealed class TasksController(
    ITaskFlowRepository repository,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpGet("api/tasks/{id:guid}", Name = nameof(GetTask))]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> GetTask(Guid id, CancellationToken cancellationToken)
    {
        var task = await repository.FindTaskAsync(id, currentUser.UserId, cancellationToken);
        return task is null ? TaskNotFound(id) : Ok(task.ToResponse());
    }

    [HttpPost("api/projects/{projectId:guid}/tasks")]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> CreateTask(
        Guid projectId,
        CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (await repository.FindProjectAsync(projectId, currentUser.UserId, cancellationToken) is null)
        {
            return ProjectNotFound(projectId);
        }

        var task = TaskItem.Create(
            projectId,
            request.Title,
            request.Description,
            timeProvider.GetUtcNow().UtcDateTime,
            request.DueDateUtc);

        await repository.AddTaskAsync(task, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return CreatedAtRoute(nameof(GetTask), new { id = task.Id }, task.ToResponse());
    }

    [HttpPut("api/tasks/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTask(
        Guid id,
        UpdateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var task = await repository.FindTaskAsync(id, currentUser.UserId, cancellationToken);
        if (task is null)
        {
            return TaskNotFound(id);
        }

        task.Update(request.Title, request.Description, request.DueDateUtc);
        await repository.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("api/tasks/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTask(Guid id, CancellationToken cancellationToken)
    {
        var task = await repository.FindTaskAsync(id, currentUser.UserId, cancellationToken);
        if (task is null)
        {
            return TaskNotFound(id);
        }

        repository.RemoveTask(task);
        await repository.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPatch("api/tasks/{id:guid}/status")]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> UpdateTaskStatus(
        Guid id,
        UpdateTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        var task = await repository.FindTaskAsync(id, currentUser.UserId, cancellationToken);
        if (task is null)
        {
            return TaskNotFound(id);
        }

        task.ChangeStatus(request.Status!.Value);
        await repository.SaveChangesAsync(cancellationToken);
        return Ok(task.ToResponse());
    }

    private NotFoundObjectResult TaskNotFound(Guid id) =>
        NotFound(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Task not found",
            Detail = $"Task '{id}' was not found.",
            Instance = HttpContext.Request.Path,
        });

    private NotFoundObjectResult ProjectNotFound(Guid id) =>
        NotFound(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Project not found",
            Detail = $"Project '{id}' was not found.",
            Instance = HttpContext.Request.Path,
        });
}
