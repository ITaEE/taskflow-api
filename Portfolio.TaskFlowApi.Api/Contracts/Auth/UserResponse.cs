namespace Portfolio.TaskFlowApi.Api.Contracts.Auth;

public sealed record UserResponse(Guid Id, string Email, DateTime CreatedAtUtc);
