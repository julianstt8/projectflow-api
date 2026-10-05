using Microsoft.IdentityModel.JsonWebTokens;
using ProjectFlow.Application.Abstractions;

namespace ProjectFlow.Api.Authentication;

internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId)
            ? userId
            : throw new InvalidOperationException("There is no authenticated user in the current request.");
}
