namespace Portfolio.TaskFlowApi.Core.Exceptions;

public sealed class CurrentUserUnavailableException : Exception
{
    public CurrentUserUnavailableException()
        : base("The authenticated user identifier is missing or invalid.")
    {
    }
}
