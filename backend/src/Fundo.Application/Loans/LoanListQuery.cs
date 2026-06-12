namespace Fundo.Application.Loans;

public sealed record LoanListQuery(
    int Page = 1,
    int PageSize = 10,
    string? Status = null,
    string? Type = null,
    string? ApplicantName = null,
    decimal? MinimumBalance = null,
    bool HighExposureOnly = false);
