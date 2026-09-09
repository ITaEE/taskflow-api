using Portfolio.TaskFlowApi.Core.Entities;

namespace Portfolio.TaskFlowApi.Core.Abstractions;

public interface ITaskFlowRepository
{
    Task<IReadOnlyList<Project>> ListProjectsAsync(Guid ownerUserId, CancellationToken cancellationToken = default);

    Task<Project?> FindProjectAsync(Guid id, Guid ownerUserId, CancellationToken cancellationToken = default);

    Task AddProjectAsync(Project project, CancellationToken cancellationToken = default);

    void RemoveProject(Project project);

    Task<IReadOnlyList<TaskItem>> ListTasksForProjectAsync(
        Guid projectId,
        Guid ownerUserId,
        CancellationToken cancellationToken = default);

    Task<TaskItem?> FindTaskAsync(Guid id, Guid ownerUserId, CancellationToken cancellationToken = default);

    Task AddTaskAsync(TaskItem task, CancellationToken cancellationToken = default);

    void RemoveTask(TaskItem task);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
