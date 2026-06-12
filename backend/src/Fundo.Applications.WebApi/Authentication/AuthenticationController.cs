using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace Fundo.Applications.WebApi.Authentication;

[ApiController]
[Route("auth")]
public sealed class AuthenticationController(TokenService tokenService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("token")]
    [EnableRateLimiting("writes")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<TokenResponse> CreateToken(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return MissingCredentials();
        }

        var token = tokenService.Authenticate(request);
        return token is null ? Unauthorized() : Ok(token);
    }

    [AllowAnonymous]
    [HttpPost("session")]
    [EnableRateLimiting("writes")]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SessionResponse>> CreateSession(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return MissingCredentials();
        }

        var principal = tokenService.AuthenticateSession(request);
        if (principal is null)
        {
            return Unauthorized();
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(tokenService.ExpiresInSeconds),
            });

        return Ok(new SessionResponse(
            principal.Identity?.Name ?? request.Username.Trim(),
            tokenService.ExpiresInSeconds,
            GetPermissions(principal)));
    }

    [Authorize]
    [HttpGet("session")]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    public ActionResult<SessionResponse> GetSession() =>
        Ok(new SessionResponse(
            User.Identity?.Name ?? string.Empty,
            tokenService.ExpiresInSeconds,
            GetPermissions(User)));

    [Authorize]
    [HttpDelete("session")]
    [EnableRateLimiting("writes")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> EndSession()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    private static BadRequestObjectResult MissingCredentials() =>
        new(new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            [nameof(LoginRequest.Username)] = ["Username and password are required."],
        }));

    private static string[] GetPermissions(ClaimsPrincipal principal) =>
        principal.FindAll(LoanPermissions.ClaimType).Select(claim => claim.Value).ToArray();
}
