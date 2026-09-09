using System.ComponentModel.DataAnnotations;
using DomainTaskStatus = Portfolio.TaskFlowApi.Core.Domain.TaskStatus;

namespace Portfolio.TaskFlowApi.Api.Contracts.Tasks;

public sealed class UpdateTaskStatusRequest
{
    [Required]
    public DomainTaskStatus? Status { get; init; }
}
