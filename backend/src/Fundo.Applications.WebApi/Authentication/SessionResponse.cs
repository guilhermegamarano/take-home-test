namespace Fundo.Applications.WebApi.Authentication;

public sealed record SessionResponse(string Username, int ExpiresIn, IReadOnlyList<string> Permissions);
