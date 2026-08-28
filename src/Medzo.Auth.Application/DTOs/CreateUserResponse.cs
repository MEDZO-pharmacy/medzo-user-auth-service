namespace Medzo.Auth.Application.DTOs;

public class CreateUserResponse
{
    public string Message { get; set; } = "User account created successfully.";
    public UserResponse User { get; set; } = null!;
}

public class PotentialDuplicateResponse
{
    public string Code { get; set; } = "potential_duplicate";
    public string Message { get; set; } =
        "A user with the same first and last name already exists. Review the match and resubmit with confirmPotentialDuplicate set to true to continue.";
    public bool ConfirmationRequired { get; set; } = true;
    public IEnumerable<UserResponse> Duplicates { get; set; } = Enumerable.Empty<UserResponse>();
}
