namespace Fundo.Application.Loans;

public sealed record LoanResponse(
    Guid Id,
    decimal Amount,
    decimal CurrentBalance,
    string Type,
    string ApplicantName,
    string Status,
    DateTimeOffset CreatedAtUtc);
