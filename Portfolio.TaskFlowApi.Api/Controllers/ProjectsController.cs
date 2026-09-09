using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.TaskFlowApi.Api.Contracts;
using Portfolio.TaskFlowApi.Api.Contracts.Projects;
using Portfolio.TaskFlowApi.Api.Contracts.Tasks;
using Portfolio.TaskFlowApi.Core.Abstractions;
using Portfolio.TaskFlowApi.Core.Entities;

namespace Portfolio.TaskFlowApi.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/projects")]
public sealed class ProjectsController(
    ITaskFlowRepository repository,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProjectResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProjectResponse>>> GetProjects(CancellationToken cancellationToken)
    {
        var projects = await repository.ListProjectsAsync(currentUser.UserId, cancellationToken);
        return Ok(projects.Select(project => project.ToResponse()).ToList());
    }

    [HttpGet("{id:guid}", Name = nameof(GetProject))]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponse>> GetProject(Guid id, CancellationToken cancellationToken)
    {
        var project = await repository.FindProjectAsync(id, currentUser.UserId, cancellationToken);
        return project is null ? ProjectNotFound(id) : Ok(project.ToResponse());
    }

    [HttpPost]
    [ProducesResponseType<ProjectResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectResponse>> CreateProject(
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var project = Project.Create(
            currentUser.UserId,
            request.Name,
            request.Description,
            timeProvider.GetUtcNow().UtcDateTime);
        await repository.AddProjectAsync(project, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return CreatedAtRoute(nameof(GetProject), new { id = project.Id }, project.ToResponse());
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProject(
        Guid id,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var project = await repository.FindProjectAsync(id, currentUser.UserId, cancellationToken);
        if (project is null)
        {
            return ProjectNotFound(id);
        }

        project.Update(request.Name, request.Description);
        await repository.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProject(Guid id, CancellationToken cancellationToken)
    {
        var project = await repository.FindProjectAsync(id, currentUser.UserId, cancellationToken);
        if (project is null)
        {
            return ProjectNotFound(id);
        }

        repository.RemoveProject(project);
        await repository.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/tasks")]
    [ProducesResponseType<IReadOnlyList<TaskResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TaskResponse>>> GetProjectTasks(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (await repository.FindProjectAsync(projectId, currentUser.UserId, cancellationToken) is null)
        {
            return ProjectNotFound(projectId);
        }

        var tasks = await repository.ListTasksForProjectAsync(projectId, currentUser.UserId, cancellationToken);
        return Ok(tasks.Select(task => task.ToResponse()).ToList());
    }

    private NotFoundObjectResult ProjectNotFound(Guid id) =>
        NotFound(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Project not found",
            Detail = $"Project '{id}' was not found.",
            Instance = HttpContext.Request.Path,
        });
}
