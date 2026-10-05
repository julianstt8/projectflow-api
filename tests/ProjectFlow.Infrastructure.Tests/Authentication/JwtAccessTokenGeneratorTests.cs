using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProjectFlow.Domain.Users;
using ProjectFlow.Infrastructure.Authentication;

namespace ProjectFlow.Infrastructure.Tests.Authentication;

public class JwtAccessTokenGeneratorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static readonly JwtOptions Options = new()
    {
        Issuer = "projectflow-api",
        Audience = "projectflow-api",
        SigningKey = new string('k', JwtOptions.MinimumSigningKeyLength),
        AccessTokenLifetimeMinutes = 15,
    };

    private static readonly User Ana =
        User.Create(Email.Create("ana@example.com").Value, "hash", "Ana Admin", Now).Value;

    [Fact]
    public async Task Token_is_signed_and_identifies_the_user_only()
    {
        var token = new JwtAccessTokenGenerator(Microsoft.Extensions.Options.Options.Create(Options), new FixedTimeProvider(Now)).Generate(Ana);

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, new TokenValidationParameters
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = Options.GetSigningKey(),
        });

        Assert.True(validation.IsValid, validation.Exception?.Message);
        var claims = validation.Claims;
        Assert.Equal(Ana.Id.ToString(), claims[JwtRegisteredClaimNames.Sub]);
        Assert.Equal("ana@example.com", claims[JwtRegisteredClaimNames.Email]);
        Assert.Equal("Ana Admin", claims[JwtRegisteredClaimNames.Name]);
        Assert.DoesNotContain(claims.Keys, key => key.Contains("role", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Token_expires_after_the_configured_lifetime()
    {
        var token = new JwtAccessTokenGenerator(Microsoft.Extensions.Options.Options.Create(Options), new FixedTimeProvider(Now)).Generate(Ana);

        Assert.Equal(Now.AddMinutes(15), token.ExpiresAt);
        var expiry = new JsonWebToken(token.Value).ValidTo;
        Assert.Equal(Now.AddMinutes(15).ToUnixTimeSeconds(), new DateTimeOffset(expiry, TimeSpan.Zero).ToUnixTimeSeconds());
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var token = new JwtAccessTokenGenerator(Microsoft.Extensions.Options.Options.Create(Options), new FixedTimeProvider(Now)).Generate(Ana);

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, new TokenValidationParameters
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = new JwtOptions { SigningKey = new string('x', JwtOptions.MinimumSigningKeyLength) }.GetSigningKey(),
        });

        Assert.False(validation.IsValid);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
