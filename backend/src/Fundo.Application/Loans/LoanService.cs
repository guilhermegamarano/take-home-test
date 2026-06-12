using Fundo.Application.Abstractions;
using Fundo.Domain.Loans;

namespace Fundo.Application.Loans;

public sealed class LoanService(
    ILoanRepository loanRepository,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILoanProductCatalog productCatalog)
{
    public async Task<LoanResponse> CreateAsync(
        CreateLoanCommand command,
        CancellationToken cancellationToken)
    {
        var product = productCatalog.Resolve(command.Type);
        LoanCommandValidator.Validate(command, product);

        var loan = Loan.Create(
            Guid.NewGuid(),
            command.Amount,
            productCatalog.ResolveLoanType(command.Type),
            command.ApplicantName!,
            clock.UtcNow);
        await loanRepository.AddAsync(loan, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(loan);
    }

    public async Task<LoanResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var loan = await loanRepository.GetByIdAsync(id, false, cancellationToken);
        return loan is null ? null : Map(loan);
    }

    public async Task<PagedResult<LoanResponse>> ListAsync(
        LoanListQuery query,
        CancellationToken cancellationToken)
    {
        LoanQueryValidator.Validate(query);

        var (loans, totalItems) = await loanRepository.ListAsync(query, cancellationToken);
        var totalPages = totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)query.PageSize);

        return new PagedResult<LoanResponse>(
            loans.Select(Map).ToArray(),
            query.Page,
            query.PageSize,
            totalItems,
            totalPages);
    }

    public async Task<LoanResponse?> MakePaymentAsync(
        Guid id,
        MakePaymentCommand command,
        CancellationToken cancellationToken)
    {
        LoanCommandValidator.Validate(command);

        var loan = await loanRepository.GetByIdAsync(id, true, cancellationToken);
        if (loan is null)
        {
            return null;
        }

        productCatalog.ValidatePayment(loan, command.Amount);
        loan.ApplyPayment(command.Amount);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(loan);
    }

    private static LoanResponse Map(Loan loan) =>
        new(
            loan.Id,
            loan.Amount,
            loan.CurrentBalance,
            FormatType(loan.Type),
            loan.ApplicantName,
            loan.Status.ToString().ToLowerInvariant(),
            loan.CreatedAtUtc);

    private static string FormatType(LoanType type) =>
        type switch
        {
            LoanType.Personal => "personal",
            LoanType.SmallBusiness => "small-business",
            LoanType.Bridge => "bridge",
            _ => throw new InvalidOperationException("Unsupported loan type."),
        };
}
