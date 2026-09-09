using Portfolio.TaskFlowApi.Api.Contracts.Auth;
using Portfolio.TaskFlowApi.Api.Contracts.Projects;
using Portfolio.TaskFlowApi.Api.Contracts.Tasks;
using Portfolio.TaskFlowApi.Core.Entities;

namespace Portfolio.TaskFlowApi.Api.Contracts;

internal static class DtoMappings
{
    public static ProjectResponse ToResponse(this Project project) =>
        new(project.Id, project.Name, project.Description, project.CreatedAtUtc);

    public static TaskResponse ToResponse(this TaskItem task) =>
        new(task.Id, task.ProjectId, task.Title, task.Description, task.Status, task.CreatedAtUtc, task.DueDateUtc);

    public static UserResponse ToResponse(this User user) =>
        new(user.Id, user.Email, user.CreatedAtUtc);
}
