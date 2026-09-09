using System.IdentityModel.Tokens.Jwt;
using Portfolio.TaskFlowApi.Core.Abstractions;
using Portfolio.TaskFlowApi.Core.Exceptions;

namespace Portfolio.TaskFlowApi.Api.Authentication;

internal sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Guid UserId
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;
            var subject = principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (principal?.Identity?.IsAuthenticated != true || !Guid.TryParse(subject, out var userId))
            {
                throw new CurrentUserUnavailableException();
            }

            return userId;
        }
    }
}
