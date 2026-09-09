using Microsoft.AspNetCore.Identity;
using Portfolio.TaskFlowApi.Core.Abstractions;
using Portfolio.TaskFlowApi.Core.Entities;

namespace Portfolio.TaskFlowApi.Infrastructure.Security;

internal sealed class AspNetPasswordHashService : IPasswordHashService
{
    private readonly PasswordHasher<User> _passwordHasher = new();
    private readonly User _dummyUser = User.Create("credential-check@invalid.local", DateTime.UtcNow);
    private readonly string _dummyHash;

    public AspNetPasswordHashService()
    {
        _dummyHash = _passwordHasher.HashPassword(_dummyUser, Guid.NewGuid().ToString("N"));
    }

    public string HashPassword(User user, string password) =>
        _passwordHasher.HashPassword(user, password);

    public PasswordVerificationOutcome VerifyPassword(User? user, string password)
    {
        var hash = user?.PasswordHash ?? _dummyHash;
        var passwordUser = user ?? _dummyUser;

        PasswordVerificationResult result;
        try
        {
            result = _passwordHasher.VerifyHashedPassword(passwordUser, hash, password);
        }
        catch (FormatException)
        {
            result = PasswordVerificationResult.Failed;
        }

        return result switch
        {
            PasswordVerificationResult.Success => PasswordVerificationOutcome.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordVerificationOutcome.SuccessRehashNeeded,
            _ => PasswordVerificationOutcome.Failed,
        };
    }
}
