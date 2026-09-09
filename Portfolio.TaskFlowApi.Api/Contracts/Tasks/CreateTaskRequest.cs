using System.ComponentModel.DataAnnotations;
using Portfolio.TaskFlowApi.Api.Validation;
using Portfolio.TaskFlowApi.Core.Domain;

namespace Portfolio.TaskFlowApi.Api.Contracts.Tasks;

public sealed class CreateTaskRequest : IValidatableObject
{
    [Required]
    [NotWhiteSpace(ErrorMessage = "Task title cannot be empty or whitespace.")]
    [MaxLength(DomainRules.TaskTitleMaxLength)]
    public string Title { get; init; } = string.Empty;

    [MaxLength(DomainRules.DescriptionMaxLength)]
    public string? Description { get; init; }

    public DateTime? DueDateUtc { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!DueDateUtc.HasValue)
        {
            yield break;
        }

        if (DueDateUtc.Value.Kind != DateTimeKind.Utc)
        {
            yield return new ValidationResult(
                "Due date must include a UTC offset (for example, 2030-01-01T12:00:00Z).",
                [nameof(DueDateUtc)]);
        }
        else if (DueDateUtc.Value <= DateTime.UtcNow)
        {
            yield return new ValidationResult("Due date must be in the future.", [nameof(DueDateUtc)]);
        }
    }
}
