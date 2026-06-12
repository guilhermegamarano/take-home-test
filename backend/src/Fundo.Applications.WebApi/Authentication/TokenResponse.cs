namespace Fundo.Applications.WebApi.Authentication;

public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn);
