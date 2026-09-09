namespace Portfolio.TaskFlowApi.Api.Contracts.Projects;

public sealed record ProjectResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTime CreatedAtUtc);
