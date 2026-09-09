using Portfolio.TaskFlowApi.Core.Domain;
using Portfolio.TaskFlowApi.Core.Exceptions;

namespace Portfolio.TaskFlowApi.Core.Entities;

public sealed class TaskItem
{
    private TaskItem()
    {
    }

    private TaskItem(
        Guid id,
        Guid projectId,
        string title,
        string? description,
        DateTime createdAtUtc,
        DateTime? dueDateUtc)
    {
        if (projectId == Guid.Empty)
        {
            throw new DomainValidationException("A task must belong to a project.");
        }

        Id = id;
        ProjectId = projectId;
        Status = Domain.TaskStatus.Todo;
        CreatedAtUtc = Project.RequireUtc(createdAtUtc, nameof(createdAtUtc));
        SetDetails(title, description, dueDateUtc);
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public Domain.TaskStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? DueDateUtc { get; private set; }

    public Project Project { get; private set; } = null!;

    public static TaskItem Create(
        Guid projectId,
        string title,
        string? description,
        DateTime createdAtUtc,
        DateTime? dueDateUtc) =>
        new(Guid.NewGuid(), projectId, title, description, createdAtUtc, dueDateUtc);

    public void Update(string title, string? description, DateTime? dueDateUtc) =>
        SetDetails(title, description, dueDateUtc);

    public void ChangeStatus(Domain.TaskStatus newStatus)
    {
        if (!Enum.IsDefined(newStatus))
        {
            throw new DomainValidationException("Task status is invalid.");
        }

        if (newStatus == Status)
        {
            return;
        }

        var transitionIsValid = (Status, newStatus) switch
        {
            (Domain.TaskStatus.Todo, Domain.TaskStatus.InProgress) => true,
            (Domain.TaskStatus.InProgress, Domain.TaskStatus.Todo) => true,
            (Domain.TaskStatus.InProgress, Domain.TaskStatus.Completed) => true,
            (Domain.TaskStatus.Completed, Domain.TaskStatus.InProgress) => true,
            _ => false,
        };

        if (!transitionIsValid)
        {
            throw new DomainValidationException($"Status cannot change directly from {Status} to {newStatus}.");
        }

        Status = newStatus;
    }

    private void SetDetails(string title, string? description, DateTime? dueDateUtc)
    {
        var normalizedTitle = title?.Trim() ?? string.Empty;
        if (normalizedTitle.Length == 0)
        {
            throw new DomainValidationException("Task title is required.");
        }

        if (normalizedTitle.Length > DomainRules.TaskTitleMaxLength)
        {
            throw new DomainValidationException($"Task title cannot exceed {DomainRules.TaskTitleMaxLength} characters.");
        }

        if (dueDateUtc.HasValue)
        {
            Project.RequireUtc(dueDateUtc.Value, nameof(dueDateUtc));
            if (dueDateUtc.Value < CreatedAtUtc)
            {
                throw new DomainValidationException("Due date cannot be earlier than the task creation date.");
            }
        }

        Title = normalizedTitle;
        Description = Project.OptionalText(description, DomainRules.DescriptionMaxLength, "Task description");
        DueDateUtc = dueDateUtc;
    }
}
