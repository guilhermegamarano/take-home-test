namespace Fundo.Applications.WebApi.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Authentication";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string SigningKey { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string ReadOnlyUsername { get; init; } = string.Empty;

    public string ReadOnlyPassword { get; init; } = string.Empty;

    public int TokenLifetimeMinutes { get; init; } = 30;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer))
        {
            throw new InvalidOperationException("Authentication issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException("Authentication audience is required.");
        }

        if (SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Authentication signing key must contain at least 32 characters.");
        }

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            throw new InvalidOperationException("Authentication credentials are required.");
        }

        if (string.IsNullOrWhiteSpace(ReadOnlyUsername) != string.IsNullOrWhiteSpace(ReadOnlyPassword))
        {
            throw new InvalidOperationException("Read-only authentication credentials must be configured together.");
        }

        if (TokenLifetimeMinutes is < 1 or > 120)
        {
            throw new InvalidOperationException("Token lifetime must be between 1 and 120 minutes.");
        }
    }
}
