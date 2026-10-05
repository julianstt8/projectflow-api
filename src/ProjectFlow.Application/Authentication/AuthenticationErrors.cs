using ProjectFlow.Domain.Common;

namespace ProjectFlow.Application.Authentication;

public static class AuthenticationErrors
{
    public static readonly Error EmailAlreadyRegistered =
        Error.Conflict("Authentication.EmailAlreadyRegistered", "An account with this e-mail already exists.");

    /// <summary>Same error for unknown e-mail, wrong password or inactive user, so accounts cannot be enumerated.</summary>
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("Authentication.InvalidCredentials", "The e-mail or password is incorrect.");
}
