namespace Portfolio.TaskFlowApi.Core.Domain;

public static class DomainRules
{
    public const int ProjectNameMaxLength = 150;
    public const int TaskTitleMaxLength = 200;
    public const int DescriptionMaxLength = 2_000;
    public const int EmailMaxLength = 254;
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;
}
