using ProjectFlow.Domain.Common;

namespace ProjectFlow.Domain.Users;

public sealed class User : Entity
{
    public const int FullNameMaxLength = 200;

    private User(Guid id, Email email, string passwordHash, string fullName, DateTimeOffset createdAt)
        : base(id)
    {
        Email = email;
        PasswordHash = passwordHash;
        FullName = fullName;
        IsActive = true;
        CreatedAt = createdAt;
    }

    public Email Email { get; private set; }

    public string PasswordHash { get; private set; }

    public string FullName { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    /// <summary>Creates a user. The password must already be hashed by the caller.</summary>
    public static Result<User> Create(Email email, string passwordHash, string fullName, DateTimeOffset now)
    {
        var name = NormalizeFullName(fullName);
        if (name.IsFailure)
        {
            return name.Error;
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return UserErrors.PasswordHashRequired;
        }

        return new User(Guid.CreateVersion7(now), email, passwordHash, name.Value, now);
    }

    public Result Rename(string fullName)
    {
        var name = NormalizeFullName(fullName);
        if (name.IsFailure)
        {
            return name.Error;
        }

        FullName = name.Value;
        return Result.Success();
    }

    public Result ChangePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return UserErrors.PasswordHashRequired;
        }

        PasswordHash = passwordHash;
        return Result.Success();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private static Result<string> NormalizeFullName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return UserErrors.FullNameRequired;
        }

        var trimmed = fullName.Trim();
        if (trimmed.Length > FullNameMaxLength)
        {
            return UserErrors.FullNameTooLong;
        }

        return trimmed;
    }
}
