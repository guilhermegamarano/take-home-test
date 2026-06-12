using Fundo.Application.Abstractions;
using Fundo.Application.Loans;
using Fundo.Domain.Loans;
using Microsoft.EntityFrameworkCore;

namespace Fundo.Infrastructure.Persistence.Repositories;

public sealed class LoanRepository(LoansDbContext dbContext) : ILoanRepository
{
    public async Task AddAsync(Loan loan, CancellationToken cancellationToken)
    {
        await dbContext.Loans.AddAsync(loan, cancellationToken);
    }

    public Task<Loan?> GetByIdAsync(
        Guid id,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = trackChanges ? dbContext.Loans : dbContext.Loans.AsNoTracking();
        return query.SingleOrDefaultAsync(loan => loan.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<Loan> Items, int TotalItems)> ListAsync(
        LoanListQuery query,
        CancellationToken cancellationToken)
    {
        var loans = ApplyFilters(dbContext.Loans.AsNoTracking(), query);
        var totalItems = await loans.CountAsync(cancellationToken);
        var items = await loans
            .OrderByDescending(loan => loan.CreatedAtUtc)
            .ThenBy(loan => loan.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalItems);
    }

    private static IQueryable<Loan> ApplyFilters(IQueryable<Loan> query, LoanListQuery filters)
    {
        if (TryParseStatus(filters.Status, out var status))
        {
            query = query.Where(loan => loan.Status == status);
        }

        if (TryParseType(filters.Type, out var type))
        {
            query = query.Where(loan => loan.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(filters.ApplicantName))
        {
            var applicantName = filters.ApplicantName.Trim();
            query = query.Where(loan => loan.ApplicantName.Contains(applicantName));
        }

        if (filters.MinimumBalance is not null)
        {
            query = query.Where(loan => loan.CurrentBalance >= filters.MinimumBalance.Value);
        }

        if (filters.HighExposureOnly)
        {
            query = query.Where(loan => loan.CurrentBalance >= 50_000m);
        }

        return query;
    }

    private static bool TryParseStatus(string? value, out LoanStatus status)
    {
        status = default;
        return value?.Trim().ToLowerInvariant() switch
        {
            "active" => Set(out status, LoanStatus.Active),
            "paid" => Set(out status, LoanStatus.Paid),
            _ => false,
        };
    }

    private static bool TryParseType(string? value, out LoanType type)
    {
        type = default;
        return value?.Trim().ToLowerInvariant() switch
        {
            "personal" => Set(out type, LoanType.Personal),
            "small-business" => Set(out type, LoanType.SmallBusiness),
            "bridge" => Set(out type, LoanType.Bridge),
            _ => false,
        };
    }

    private static bool Set<T>(out T target, T value)
    {
        target = value;
        return true;
    }
}
