using System.Net.Mail;
using Portfolio.TaskFlowApi.Core.Domain;
using Portfolio.TaskFlowApi.Core.Exceptions;

namespace Portfolio.TaskFlowApi.Core.Entities;

public sealed class User
{
    private readonly List<Project> _projects = [];

    private User()
    {
    }

    private User(Guid id, string email, DateTime createdAtUtc)
    {
        Id = id;
        Email = NormalizeEmail(email);
        NormalizedEmail = Email.ToUpperInvariant();
        CreatedAtUtc = Project.RequireUtc(createdAtUtc, nameof(createdAtUtc));
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<Project> Projects => _projects.AsReadOnly();

    public static User Create(string email, DateTime createdAtUtc) =>
        new(Guid.NewGuid(), email, createdAtUtc);

    public static string NormalizeEmail(string email)
    {
        var normalized = email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw new DomainValidationException("Email is required.");
        }

        if (normalized.Length > DomainRules.EmailMaxLength)
        {
            throw new DomainValidationException($"Email cannot exceed {DomainRules.EmailMaxLength} characters.");
        }

        if (!MailAddress.TryCreate(normalized, out var parsed) ||
            !string.Equals(parsed.Address, normalized, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainValidationException("Email format is invalid.");
        }

        return normalized;
    }

    public static void ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new DomainValidationException("Password is required.");
        }

        if (password.Length < DomainRules.PasswordMinLength)
        {
            throw new DomainValidationException($"Password must contain at least {DomainRules.PasswordMinLength} characters.");
        }

        if (password.Length > DomainRules.PasswordMaxLength)
        {
            throw new DomainValidationException($"Password cannot exceed {DomainRules.PasswordMaxLength} characters.");
        }
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainValidationException("Password hash is required.");
        }

        PasswordHash = passwordHash;
    }
}
