using DomainTaskStatus = Portfolio.TaskFlowApi.Core.Domain.TaskStatus;

namespace Portfolio.TaskFlowApi.Api.Contracts.Tasks;

public sealed record TaskResponse(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    DomainTaskStatus Status,
    DateTime CreatedAtUtc,
    DateTime? DueDateUtc);
