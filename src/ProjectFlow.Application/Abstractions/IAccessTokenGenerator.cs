using ProjectFlow.Domain.Users;

namespace ProjectFlow.Application.Abstractions;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>
/// Issues short-lived access tokens. They identify the user only: roles are resolved per request
/// from the database (PRD 4.1), because they change and depend on the project.
/// </summary>
public interface IAccessTokenGenerator
{
    AccessToken Generate(User user);
}
