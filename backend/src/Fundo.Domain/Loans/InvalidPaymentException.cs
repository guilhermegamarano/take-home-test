namespace Fundo.Domain.Loans;

public sealed class InvalidPaymentException(string message) : LoanException(message);
