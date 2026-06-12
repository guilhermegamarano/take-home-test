using Fundo.Application.Abstractions;
using Fundo.Application.Loans;
using Fundo.Domain.Loans;
using Fundo.Infrastructure.Persistence;
using Fundo.Infrastructure.Persistence.Repositories;
using Fundo.Infrastructure.Tests.Infrastructure;
using Fundo.Infrastructure.Time;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Fundo.Infrastructure.Tests;

[Collection(SqlServerTestGroup.Name)]
public sealed class LoanPersistenceTests(DockerSqlServerFixture sqlServer)
{
    [Fact]
    public async Task MigrationShouldCreateSchemaAndSeedLoans()
    {
        await using var context = await CreateMigratedContextAsync();

        var loans = await context.Loans.AsNoTracking().ToListAsync();

        Assert.Equal(5, loans.Count);
        Assert.Contains(loans, loan => loan.ApplicantName == "John Doe");
        Assert.Contains(loans, loan => loan.Status == LoanStatus.Paid && loan.CurrentBalance == 0m);
    }

    [Fact]
    public async Task RepositoryShouldPersistAndQueryLoans()
    {
        await using var context = await CreateMigratedContextAsync();
        var repository = new LoanRepository(context);
        var loan = Loan.Create(
            Guid.NewGuid(),
            1_500m,
            LoanType.Personal,
            "Maria Silva",
            new DateTimeOffset(2026, 6, 11, 12, 0, 0, TimeSpan.Zero));

        await repository.AddAsync(loan, CancellationToken.None);
        await ((IUnitOfWork)context).SaveChangesAsync(CancellationToken.None);

        await using var verificationContext = CreateContext(context.Database.GetConnectionString()!);
        var verificationRepository = new LoanRepository(verificationContext);
        var persisted = await verificationRepository.GetByIdAsync(loan.Id, false, CancellationToken.None);
        var listed = await verificationRepository.ListAsync(new LoanListQuery(), CancellationToken.None);

        Assert.NotNull(persisted);
        Assert.Contains(listed.Items, item => item.Id == loan.Id);
    }

    [Fact]
    public async Task DatabaseConstraintsShouldRejectInvalidBalances()
    {
        await using var context = await CreateMigratedContextAsync();

        var exception = await Assert.ThrowsAsync<SqlException>(() =>
            context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Loans
                    (Id, Amount, CurrentBalance, Type, ApplicantName, Status, CreatedAtUtc)
                VALUES
                    ({0}, {1}, {2}, {3}, {4}, {5}, {6})
                """,
                Guid.NewGuid(),
                100m,
                -1m,
                "Personal",
                "Invalid Balance",
                "Active",
                new DateTimeOffset(2026, 6, 11, 12, 0, 0, TimeSpan.Zero)));

        Assert.Contains("CK_Loans_CurrentBalance_Valid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConcurrentPaymentsShouldReturnApplicationConflict()
    {
        await using var setupContext = await CreateMigratedContextAsync();
        var loan = Loan.Create(
            Guid.NewGuid(),
            1_000m,
            LoanType.Personal,
            "Concurrent Payment",
            new DateTimeOffset(2026, 6, 11, 12, 0, 0, TimeSpan.Zero));
        setupContext.Loans.Add(loan);
        await setupContext.SaveChangesAsync();

        var connectionString = setupContext.Database.GetConnectionString()!;
        await using var firstContext = CreateContext(connectionString);
        await using var secondContext = CreateContext(connectionString);
        var firstRepository = new LoanRepository(firstContext);
        var secondRepository = new LoanRepository(secondContext);

        var first = await firstRepository.GetByIdAsync(loan.Id, true, CancellationToken.None);
        var second = await secondRepository.GetByIdAsync(loan.Id, true, CancellationToken.None);
        Assert.NotNull(first);
        Assert.NotNull(second);

        first.ApplyPayment(100m);
        await ((IUnitOfWork)firstContext).SaveChangesAsync(CancellationToken.None);

        second.ApplyPayment(50m);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            ((IUnitOfWork)secondContext).SaveChangesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DatabaseInitializerShouldApplyMigrationsFromServiceProvider()
    {
        var connectionString = sqlServer.CreateDatabaseConnectionString(
            $"LoanManagement_{Guid.NewGuid():N}");
        await using var services = new ServiceCollection()
            .AddDbContext<LoansDbContext>(options => options.UseSqlServer(connectionString))
            .BuildServiceProvider();

        await services.ApplyMigrationsAsync();

        await using var context = CreateContext(connectionString);
        Assert.Equal(5, await context.Loans.CountAsync());
    }

    [Fact]
    public async Task SqlServerHealthCheckShouldReportHealthyWhenDatabaseConnects()
    {
        await using var context = await CreateMigratedContextAsync();
        var healthCheck = new Fundo.Applications.WebApi.Health.SqlServerHealthCheck(context);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task SqlServerHealthCheckShouldReportUnhealthyWhenDatabaseFails()
    {
        var connectionString = new SqlConnectionStringBuilder(sqlServer.MasterConnectionString)
        {
            DataSource = "127.0.0.1,1",
            ConnectTimeout = 1,
        }.ConnectionString;
        await using var context = CreateContext(connectionString);
        var healthCheck = new Fundo.Applications.WebApi.Health.SqlServerHealthCheck(context);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public void SystemClockShouldReturnUtcTimestamp()
    {
        var timestamp = new SystemClock().UtcNow;

        Assert.Equal(TimeSpan.Zero, timestamp.Offset);
    }

    private async Task<LoansDbContext> CreateMigratedContextAsync()
    {
        var databaseName = $"LoanManagement_{Guid.NewGuid():N}";
        var context = CreateContext(sqlServer.CreateDatabaseConnectionString(databaseName));
        await context.Database.MigrateAsync();
        return context;
    }

    private static LoansDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<LoansDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new LoansDbContext(options);
    }
}
