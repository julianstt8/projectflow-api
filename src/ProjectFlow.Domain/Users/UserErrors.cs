using ProjectFlow.Domain.Common;

namespace ProjectFlow.Domain.Users;

public static class UserErrors
{
    public static readonly Error EmailRequired =
        Error.Validation("User.EmailRequired", "The e-mail is required.");

    public static readonly Error EmailInvalid =
        Error.Validation("User.EmailInvalid", $"The e-mail is not valid or exceeds {Email.MaxLength} characters.");

    public static readonly Error FullNameRequired =
        Error.Validation("User.FullNameRequired", "The full name is required.");

    public static readonly Error FullNameTooLong =
        Error.Validation("User.FullNameTooLong", $"The full name cannot exceed {User.FullNameMaxLength} characters.");

    public static readonly Error PasswordHashRequired =
        Error.Validation("User.PasswordHashRequired", "The password hash is required.");
}
