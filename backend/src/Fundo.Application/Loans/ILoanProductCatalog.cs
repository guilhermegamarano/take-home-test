using Fundo.Domain.Loans;

namespace Fundo.Application.Loans;

public interface ILoanProductCatalog
{
    LoanProductRule Resolve(string? type);

    LoanType ResolveLoanType(string? type);

    void ValidatePayment(Loan loan, decimal paymentAmount);
}
