namespace Portfolio.TaskFlowApi.Core.Exceptions;

public sealed class DuplicateEmailException : Exception
{
    public DuplicateEmailException()
        : base("An account with this email already exists.")
    {
    }
}
