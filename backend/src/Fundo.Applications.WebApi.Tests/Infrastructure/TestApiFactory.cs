using Fundo.Application.Abstractions;
using Fundo.Application.Loans;
using Fundo.Domain.Loans;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fundo.Applications.WebApi.Tests.Infrastructure;

public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    public const string Username = "reviewer";
    public const string Password = "test-password";
    public const string ReadOnlyUsername = "auditor";
    public const string ReadOnlyPassword = "read-only-test-password";
    public static readonly Guid ActiveSeedLoanId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid PaidSeedLoanId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    public FakeLoanStore Store { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:LoansDatabase", "Server=localhost;Database=unused");
        builder.UseSetting("Authentication:Issuer", "Fundo.Tests");
        builder.UseSetting("Authentication:Audience", "Fundo.Tests.Client");
        builder.UseSetting("Authentication:SigningKey", "test-signing-key-with-at-least-32-characters");
        builder.UseSetting("Authentication:Username", Username);
        builder.UseSetting("Authentication:Password", Password);
        builder.UseSetting("Authentication:ReadOnlyUsername", ReadOnlyUsername);
        builder.UseSetting("Authentication:ReadOnlyPassword", ReadOnlyPassword);
        builder.UseSetting("Authentication:TokenLifetimeMinutes", "30");
        builder.UseSetting("RateLimiting:WritePermitLimit", "1000");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ILoanRepository>();
            services.RemoveAll<IUnitOfWork>();
            services.RemoveAll<IClock>();
            services.AddSingleton(Store);
            services.AddSingleton<ILoanRepository>(Store);
            services.AddSingleton<IUnitOfWork>(Store);
            services.AddSingleton<IClock>(new FakeClock());
        });
    }
}

public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; } = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);
}

public sealed class FakeLoanStore : ILoanRepository, IUnitOfWork
{
    private readonly Dictionary<Guid, Loan> loans = [];
    private readonly object sync = new();

    public FakeLoanStore()
    {
        Reset();
    }

    public void Reset()
    {
        var seed = Loan.Create(
            TestApiFactory.ActiveSeedLoanId,
            25_000m,
            LoanType.Personal,
            "John Doe",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        seed.ApplyPayment(6_250m);

        var paidSeed = Loan.Create(
            TestApiFactory.PaidSeedLoanId,
            15_000m,
            LoanType.Personal,
            "Jane Smith",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        paidSeed.ApplyPayment(15_000m);

        lock (sync)
        {
            loans.Clear();
            loans.Add(seed.Id, seed);
            loans.Add(paidSeed.Id, paidSeed);
        }
    }

    public Task AddAsync(Loan loan, CancellationToken cancellationToken)
    {
        lock (sync)
        {
            loans.Add(loan.Id, loan);
        }

        return Task.CompletedTask;
    }

    public Task<Loan?> GetByIdAsync(
        Guid id,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        lock (sync)
        {
            return Task.FromResult(loans.GetValueOrDefault(id));
        }
    }

    public Task<(IReadOnlyList<Loan> Items, int TotalItems)> ListAsync(
        LoanListQuery query,
        CancellationToken cancellationToken)
    {
        lock (sync)
        {
            var filtered = loans.Values.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                filtered = filtered.Where(loan =>
                    string.Equals(loan.Status.ToString(), query.Status, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query.Type))
            {
                filtered = filtered.Where(loan =>
                    string.Equals(FormatType(loan.Type), query.Type, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query.ApplicantName))
            {
                filtered = filtered.Where(loan =>
                    loan.ApplicantName.Contains(query.ApplicantName, StringComparison.OrdinalIgnoreCase));
            }

            if (query.MinimumBalance is not null)
            {
                filtered = filtered.Where(loan => loan.CurrentBalance >= query.MinimumBalance.Value);
            }

            if (query.HighExposureOnly)
            {
                filtered = filtered.Where(loan => loan.CurrentBalance >= 50_000m);
            }

            var ordered = filtered
                .OrderByDescending(loan => loan.CreatedAtUtc)
                .ThenBy(loan => loan.Id)
                .ToArray();
            var items = ordered
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToArray();

            return Task.FromResult<(IReadOnlyList<Loan>, int)>((items, ordered.Length));
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static string FormatType(LoanType type) =>
        type switch
        {
            LoanType.Personal => "personal",
            LoanType.SmallBusiness => "small-business",
            LoanType.Bridge => "bridge",
            _ => throw new InvalidOperationException("Unsupported loan type."),
        };
}
