namespace Portfolio.TaskFlowApi.Core.Exceptions;

public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("Email or password is invalid.")
    {
    }
}
