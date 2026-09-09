using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.TaskFlowApi.Api.Contracts;
using Portfolio.TaskFlowApi.Api.Contracts.Auth;
using Portfolio.TaskFlowApi.Core.Abstractions;

namespace Portfolio.TaskFlowApi.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/auth")]
public sealed class AuthController(IAuthenticationService authenticationService) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var user = await authenticationService.RegisterAsync(request.Email, request.Password, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, user.ToResponse());
    }

    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.LoginAsync(request.Email, request.Password, cancellationToken);
        return Ok(new AuthResponse(
            result.AccessToken.Token,
            result.AccessToken.ExpiresAtUtc,
            result.User.ToResponse()));
    }
}
