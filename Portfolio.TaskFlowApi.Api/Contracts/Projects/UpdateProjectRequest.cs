using System.ComponentModel.DataAnnotations;
using Portfolio.TaskFlowApi.Api.Validation;
using Portfolio.TaskFlowApi.Core.Domain;

namespace Portfolio.TaskFlowApi.Api.Contracts.Projects;

public sealed class UpdateProjectRequest
{
    [Required]
    [NotWhiteSpace(ErrorMessage = "Project name cannot be empty or whitespace.")]
    [MaxLength(DomainRules.ProjectNameMaxLength)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(DomainRules.DescriptionMaxLength)]
    public string? Description { get; init; }
}
