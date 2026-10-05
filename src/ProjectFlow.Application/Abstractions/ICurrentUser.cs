namespace ProjectFlow.Application.Abstractions;

/// <summary>The authenticated user of the current request (from the access token).</summary>
public interface ICurrentUser
{
    /// <summary>Id of the authenticated user. Only call it from authenticated endpoints.</summary>
    Guid UserId { get; }
}
