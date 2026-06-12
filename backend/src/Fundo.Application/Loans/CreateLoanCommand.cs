namespace Fundo.Application.Loans;

public sealed record CreateLoanCommand(decimal Amount, string? ApplicantName, string? Type = null);
