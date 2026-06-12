using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Fundo.Applications.WebApi.Authentication;

public sealed class TokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions jwtOptions = options.Value;

    public TokenResponse? Authenticate(LoginRequest request)
    {
        var identity = ResolveIdentity(request);
        if (identity is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(jwtOptions.TokenLifetimeMinutes);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            jwtOptions.Issuer,
            jwtOptions.Audience,
            CreateClaims(identity),
            now.UtcDateTime,
            expiresAt.UtcDateTime,
            credentials);

        return new TokenResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            checked((int)(expiresAt - now).TotalSeconds));
    }

    public ClaimsPrincipal? AuthenticateSession(LoginRequest request)
    {
        var identity = ResolveIdentity(request);
        if (identity is null)
        {
            return null;
        }

        return new ClaimsPrincipal(new ClaimsIdentity(
            CreateClaims(identity),
            "ApplicationCookie"));
    }

    public int ExpiresInSeconds => checked(jwtOptions.TokenLifetimeMinutes * 60);

    public string Username => jwtOptions.Username;

    private AuthenticatedIdentity? ResolveIdentity(LoginRequest request)
    {
        if (SecureEquals(request.Username, jwtOptions.Username) &&
            SecureEquals(request.Password, jwtOptions.Password))
        {
            return new AuthenticatedIdentity(jwtOptions.Username, [LoanPermissions.Read, LoanPermissions.Write]);
        }

        if (!string.IsNullOrWhiteSpace(jwtOptions.ReadOnlyUsername) &&
            SecureEquals(request.Username, jwtOptions.ReadOnlyUsername) &&
            SecureEquals(request.Password, jwtOptions.ReadOnlyPassword))
        {
            return new AuthenticatedIdentity(jwtOptions.ReadOnlyUsername, [LoanPermissions.Read]);
        }

        return null;
    }

    private static Claim[] CreateClaims(AuthenticatedIdentity identity) =>
        [
            new Claim(JwtRegisteredClaimNames.Sub, identity.Username),
            new Claim(ClaimTypes.Name, identity.Username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            .. identity.Permissions.Select(permission => new Claim(LoanPermissions.ClaimType, permission)),
        ];

    private static bool SecureEquals(string? provided, string expected)
    {
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided ?? string.Empty));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
    }

    private sealed record AuthenticatedIdentity(string Username, IReadOnlyList<string> Permissions);
}
