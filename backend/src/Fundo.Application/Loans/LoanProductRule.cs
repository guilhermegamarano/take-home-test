namespace Fundo.Application.Loans;

public sealed record LoanProductRule(
    string Type,
    decimal MinimumAmount,
    decimal MaximumAmount,
    decimal MinimumPayment,
    bool Enabled);
