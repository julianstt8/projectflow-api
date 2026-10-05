using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProjectFlow.Infrastructure.Authentication;

namespace ProjectFlow.Api.Authentication;

internal static class AuthenticationSetup
{
    /// <summary>
    /// JWT bearer authentication, validated with the same <see cref="JwtOptions"/> used to issue tokens.
    /// Every endpoint requires an authenticated user unless it is marked <c>[AllowAnonymous]</c>.
    /// </summary>
    public static WebApplicationBuilder AddJwtAuthentication(this WebApplicationBuilder builder)
    {
        UseEphemeralSigningKeyInDevelopment(builder);

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        builder.Services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = jwt.Value.GetSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                };
            });

        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return builder;
    }

    /// <summary>
    /// In Development, with no key configured, use a random key for this run only (tokens stop working
    /// after a restart). No key is ever committed. Outside Development a missing key stops the startup.
    /// </summary>
    private static void UseEphemeralSigningKeyInDevelopment(WebApplicationBuilder builder)
    {
        var key = $"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)}";

        if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(builder.Configuration[key]))
        {
            builder.Configuration[key] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }
    }
}
