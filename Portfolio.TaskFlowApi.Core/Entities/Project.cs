using Portfolio.TaskFlowApi.Core.Domain;
using Portfolio.TaskFlowApi.Core.Exceptions;

namespace Portfolio.TaskFlowApi.Core.Entities;

public sealed class Project
{
    private readonly List<TaskItem> _tasks = [];

    private Project()
    {
    }

    private Project(Guid id, Guid ownerUserId, string name, string? description, DateTime createdAtUtc)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new DomainValidationException("A project must have an owner.");
        }

        Id = id;
        OwnerUserId = ownerUserId;
        CreatedAtUtc = RequireUtc(createdAtUtc, nameof(createdAtUtc));
        SetDetails(name, description);
    }

    public Guid Id { get; private set; }

    public Guid OwnerUserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public User Owner { get; private set; } = null!;

    public IReadOnlyCollection<TaskItem> Tasks => _tasks.AsReadOnly();

    public static Project Create(Guid ownerUserId, string name, string? description, DateTime createdAtUtc) =>
        new(Guid.NewGuid(), ownerUserId, name, description, createdAtUtc);

    public void Update(string name, string? description) => SetDetails(name, description);

    private void SetDetails(string name, string? description)
    {
        Name = RequiredText(name, DomainRules.ProjectNameMaxLength, "Project name");
        Description = OptionalText(description, DomainRules.DescriptionMaxLength, "Project description");
    }

    private static string RequiredText(string value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw new DomainValidationException($"{fieldName} is required.");
        }

        if (normalized.Length > maxLength)
        {
            throw new DomainValidationException($"{fieldName} cannot exceed {maxLength} characters.");
        }

        return normalized;
    }

    internal static string? OptionalText(string? value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw new DomainValidationException($"{fieldName} cannot exceed {maxLength} characters.");
        }

        return normalized;
    }

    internal static DateTime RequireUtc(DateTime value, string fieldName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new DomainValidationException($"{fieldName} must be a UTC date and time.");
        }

        return value;
    }
}
