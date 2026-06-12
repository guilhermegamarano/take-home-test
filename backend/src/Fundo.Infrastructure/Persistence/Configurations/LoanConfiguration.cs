using Fundo.Domain.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fundo.Infrastructure.Persistence.Configurations;

public sealed class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    private static readonly DateTimeOffset SeedCreatedAtUtc =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.ToTable("Loans", table =>
        {
            table.HasCheckConstraint("CK_Loans_Amount_Positive", "[Amount] > 0");
            table.HasCheckConstraint(
                "CK_Loans_CurrentBalance_Valid",
                "[CurrentBalance] >= 0 AND [CurrentBalance] <= [Amount]");
            table.HasCheckConstraint(
                "CK_Loans_Status_Valid",
                "[Status] IN ('Active', 'Paid')");
            table.HasCheckConstraint(
                "CK_Loans_Type_Valid",
                "[Type] IN ('Personal', 'SmallBusiness', 'Bridge')");
            table.HasCheckConstraint(
                "CK_Loans_Status_Balance_Consistent",
                "([Status] = 'Active' AND [CurrentBalance] > 0) OR " +
                "([Status] = 'Paid' AND [CurrentBalance] = 0)");
        });
        builder.HasKey(loan => loan.Id);

        builder.Property(loan => loan.Amount).HasPrecision(18, 2);
        builder.Property(loan => loan.CurrentBalance).HasPrecision(18, 2);
        builder.Property(loan => loan.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(loan => loan.ApplicantName)
            .HasMaxLength(Loan.ApplicantNameMaximumLength)
            .IsRequired();
        builder.Property(loan => loan.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(loan => loan.CreatedAtUtc).IsRequired();
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.HasIndex(loan => loan.CreatedAtUtc);
        builder.HasIndex(loan => loan.Status);
        builder.HasIndex(loan => loan.Type);

        builder.HasData(
            CreateSeed("11111111-1111-1111-1111-111111111111", 25_000m, 18_750m, LoanType.Personal, "John Doe", LoanStatus.Active),
            CreateSeed("22222222-2222-2222-2222-222222222222", 15_000m, 0m, LoanType.Personal, "Jane Smith", LoanStatus.Paid),
            CreateSeed("33333333-3333-3333-3333-333333333333", 50_000m, 32_500m, LoanType.SmallBusiness, "Robert Johnson", LoanStatus.Active),
            CreateSeed("44444444-4444-4444-4444-444444444444", 10_000m, 0m, LoanType.Personal, "Emily Williams", LoanStatus.Paid),
            CreateSeed("55555555-5555-5555-5555-555555555555", 75_000m, 72_000m, LoanType.Bridge, "Michael Brown", LoanStatus.Active));
    }

    private static object CreateSeed(
        string id,
        decimal amount,
        decimal currentBalance,
        LoanType type,
        string applicantName,
        LoanStatus status) =>
        new
        {
            Id = Guid.Parse(id),
            Amount = amount,
            CurrentBalance = currentBalance,
            Type = type,
            ApplicantName = applicantName,
            Status = status,
            CreatedAtUtc = SeedCreatedAtUtc,
        };
}
