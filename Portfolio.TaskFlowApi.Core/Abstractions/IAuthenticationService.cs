using Portfolio.TaskFlowApi.Core.Entities;

namespace Portfolio.TaskFlowApi.Core.Abstractions;

public interface IAuthenticationService
{
    Task<User> RegisterAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<AuthenticationResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
}

public sealed record AuthenticationResult(User User, AccessTokenResult AccessToken);
