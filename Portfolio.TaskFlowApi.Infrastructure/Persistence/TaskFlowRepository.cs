using Microsoft.EntityFrameworkCore;
using Portfolio.TaskFlowApi.Core.Abstractions;
using Portfolio.TaskFlowApi.Core.Entities;

namespace Portfolio.TaskFlowApi.Infrastructure.Persistence;

internal sealed class TaskFlowRepository(TaskFlowDbContext dbContext) : ITaskFlowRepository
{
    public async Task<IReadOnlyList<Project>> ListProjectsAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.OwnerUserId == ownerUserId)
            .OrderBy(project => project.CreatedAtUtc)
            .ThenBy(project => project.Id)
            .ToListAsync(cancellationToken);

    public Task<Project?> FindProjectAsync(
        Guid id,
        Guid ownerUserId,
        CancellationToken cancellationToken = default) =>
        dbContext.Projects.SingleOrDefaultAsync(
            project => project.Id == id && project.OwnerUserId == ownerUserId,
            cancellationToken);

    public async Task AddProjectAsync(Project project, CancellationToken cancellationToken = default) =>
        await dbContext.Projects.AddAsync(project, cancellationToken);

    public void RemoveProject(Project project) => dbContext.Projects.Remove(project);

    public async Task<IReadOnlyList<TaskItem>> ListTasksForProjectAsync(
        Guid projectId,
        Guid ownerUserId,
        CancellationToken cancellationToken = default) =>
        await dbContext.TaskItems
            .AsNoTracking()
            .Where(task => task.ProjectId == projectId && task.Project.OwnerUserId == ownerUserId)
            .OrderBy(task => task.CreatedAtUtc)
            .ThenBy(task => task.Id)
            .ToListAsync(cancellationToken);

    public Task<TaskItem?> FindTaskAsync(
        Guid id,
        Guid ownerUserId,
        CancellationToken cancellationToken = default) =>
        dbContext.TaskItems.SingleOrDefaultAsync(
            task => task.Id == id && task.Project.OwnerUserId == ownerUserId,
            cancellationToken);

    public async Task AddTaskAsync(TaskItem task, CancellationToken cancellationToken = default) =>
        await dbContext.TaskItems.AddAsync(task, cancellationToken);

    public void RemoveTask(TaskItem task) => dbContext.TaskItems.Remove(task);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}
