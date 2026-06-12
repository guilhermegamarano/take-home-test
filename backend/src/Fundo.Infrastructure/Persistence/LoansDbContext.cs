using Fundo.Application.Abstractions;
using Fundo.Domain.Loans;
using Microsoft.EntityFrameworkCore;

namespace Fundo.Infrastructure.Persistence;

public sealed class LoansDbContext(DbContextOptions<LoansDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Loan> Loans => Set<Loan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LoansDbContext).Assembly);
    }

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(
                "The loan was changed by another request. Reload it and try again.",
                exception);
        }
    }
}
