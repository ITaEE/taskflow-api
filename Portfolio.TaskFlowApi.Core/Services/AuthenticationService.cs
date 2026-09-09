using Portfolio.TaskFlowApi.Core.Abstractions;
using Portfolio.TaskFlowApi.Core.Entities;
using Portfolio.TaskFlowApi.Core.Exceptions;

namespace Portfolio.TaskFlowApi.Core.Services;

public sealed class AuthenticationService(
    IUserRepository userRepository,
    IPasswordHashService passwordHashService,
    IAccessTokenService accessTokenService,
    TimeProvider timeProvider) : IAuthenticationService
{
    public async Task<User> RegisterAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = User.NormalizeEmail(email).ToUpperInvariant();
        User.ValidatePassword(password);

        if (await userRepository.FindByNormalizedEmailAsync(normalizedEmail, cancellationToken) is not null)
        {
            throw new DuplicateEmailException();
        }

        var user = User.Create(email, timeProvider.GetUtcNow().UtcDateTime);
        user.SetPasswordHash(passwordHashService.HashPassword(user, password));

        await userRepository.AddAsync(user, cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<AuthenticationResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        User? user = null;

        try
        {
            var normalizedEmail = User.NormalizeEmail(email).ToUpperInvariant();
            user = await userRepository.FindByNormalizedEmailAsync(normalizedEmail, cancellationToken);
        }
        catch (DomainValidationException)
        {
            // Continue through the same credential-verification path used for unknown users.
        }

        var verification = passwordHashService.VerifyPassword(user, password);
        if (user is null || verification == PasswordVerificationOutcome.Failed)
        {
            throw new InvalidCredentialsException();
        }

        if (verification == PasswordVerificationOutcome.SuccessRehashNeeded)
        {
            user.SetPasswordHash(passwordHashService.HashPassword(user, password));
            await userRepository.SaveChangesAsync(cancellationToken);
        }

        var issuedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        return new AuthenticationResult(user, accessTokenService.CreateToken(user, issuedAtUtc));
    }
}
