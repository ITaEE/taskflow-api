using System.ComponentModel.DataAnnotations;
using Portfolio.TaskFlowApi.Core.Domain;

namespace Portfolio.TaskFlowApi.Api.Contracts.Auth;

public sealed class RegisterRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(DomainRules.EmailMaxLength)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(DomainRules.PasswordMinLength)]
    [MaxLength(DomainRules.PasswordMaxLength)]
    public string Password { get; init; } = string.Empty;
}
