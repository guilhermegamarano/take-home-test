namespace Fundo.Domain.Loans;

public sealed class InvalidLoanException(string message) : LoanException(message);
