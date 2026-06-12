using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fundo.Infrastructure.Persistence;

public sealed class LoansDbContextFactory : IDesignTimeDbContextFactory<LoansDbContext>
{
    public LoansDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LoansDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=LoanManagement;Integrated Security=true;TrustServerCertificate=true")
            .Options;

        return new LoansDbContext(options);
    }
}
