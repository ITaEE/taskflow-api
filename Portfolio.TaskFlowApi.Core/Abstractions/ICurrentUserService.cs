namespace Portfolio.TaskFlowApi.Core.Abstractions;

public interface ICurrentUserService
{
    Guid UserId { get; }
}
