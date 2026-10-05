namespace ProjectFlow.Application.Abstractions;

/// <summary>A new refresh token: <see cref="Value"/> goes to the client once; only <see cref="Hash"/> is stored.</summary>
public sealed record GeneratedRefreshToken(string Value, string Hash);

public interface IRefreshTokenService
{
    TimeSpan Lifetime { get; }

    GeneratedRefreshToken Generate();

    string Hash(string refreshToken);
}
