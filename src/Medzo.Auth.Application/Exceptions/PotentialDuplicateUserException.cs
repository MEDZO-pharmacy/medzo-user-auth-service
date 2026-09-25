using Medzo.Auth.Application.DTOs;

namespace Medzo.Auth.Application.Exceptions;

public class PotentialDuplicateUserException : Exception
{
    public PotentialDuplicateUserException(IEnumerable<UserResponse> duplicates)
        : base("A user with the same first and last name already exists.")
    {
        Duplicates = duplicates.ToArray();
    }

    public IReadOnlyCollection<UserResponse> Duplicates { get; }
}
