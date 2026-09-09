using Portfolio.TaskFlowApi.Core.Entities;

namespace Portfolio.TaskFlowApi.Core.Abstractions;

public interface IPasswordHashService
{
    string HashPassword(User user, string password);

    PasswordVerificationOutcome VerifyPassword(User? user, string password);
}

public enum PasswordVerificationOutcome
{
    Failed = 0,
    Success = 1,
    SuccessRehashNeeded = 2,
}
