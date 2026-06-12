using Fundo.Application.Loans;
using Fundo.Domain.Loans;

namespace Fundo.Application.Abstractions;

public interface ILoanRepository
{
    Task AddAsync(Loan loan, CancellationToken cancellationToken);

    Task<Loan?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Loan> Items, int TotalItems)> ListAsync(
        LoanListQuery query,
        CancellationToken cancellationToken);
}
