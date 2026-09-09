using Portfolio.TaskFlowApi.Core.Entities;

namespace Portfolio.TaskFlowApi.Core.Abstractions;

public interface IAccessTokenService
{
    AccessTokenResult CreateToken(User user, DateTime issuedAtUtc);
}

public sealed record AccessTokenResult(string Token, DateTime ExpiresAtUtc);
