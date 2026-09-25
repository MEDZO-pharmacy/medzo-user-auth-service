namespace Medzo.Auth.Application.Exceptions;

public class UserConflictException : Exception
{
    public UserConflictException(string message) : base(message)
    {
    }

    public UserConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
