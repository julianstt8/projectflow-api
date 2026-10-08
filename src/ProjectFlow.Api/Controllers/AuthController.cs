using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using ProjectFlow.Application.Authentication;
using ProjectFlow.Application.Authentication.Login;
using ProjectFlow.Application.Authentication.Logout;
using ProjectFlow.Application.Authentication.Refresh;
using ProjectFlow.Application.Authentication.Register;

namespace ProjectFlow.Api.Controllers;

/// <summary>Registration, login, token refresh and logout (RF-01).</summary>
/// <param name="sender">Sends commands and queries to their handlers.</param>
[Route("api/auth")]
public sealed class AuthController(ISender sender) : ApiControllerBase
{
    /// <summary>Creates an account (RF-01).</summary>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : ErrorResult(result.Error);
    }

    /// <summary>Exchanges e-mail and password for a short-lived access token and a refresh token (RF-01).</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error);
    }

    /// <summary>Exchanges a refresh token for a new token pair. The refresh token can be used only once.</summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error);
    }

    /// <summary>Ends the session of the refresh token. Anonymous, so it works after the access token expired.</summary>
    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout(LogoutCommand command, CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken);

        return NoContent();
    }

    /// <summary>The user identified by the access token.</summary>
    [HttpGet("me")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<CurrentUserResponse> Me() =>
        new CurrentUserResponse(
            Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value),
            User.FindFirst(JwtRegisteredClaimNames.Email)!.Value,
            User.FindFirst(JwtRegisteredClaimNames.Name)!.Value);
}

/// <summary>The user identified by the access token.</summary>
/// <param name="Id">User id.</param>
/// <param name="Email">E-mail of the user.</param>
/// <param name="FullName">Full name of the user.</param>
public sealed record CurrentUserResponse(Guid Id, string Email, string FullName);
