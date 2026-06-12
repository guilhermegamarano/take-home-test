using Fundo.Domain.Loans;

namespace Fundo.Domain.Tests.Loans;

public sealed class LoanTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateWithValidInputShouldCreateActiveLoanWithFullBalance()
    {
        var loan = Loan.Create(Guid.NewGuid(), 1_500.00m, LoanType.Personal, "  Maria Silva  ", CreatedAtUtc);

        Assert.Equal(1_500.00m, loan.Amount);
        Assert.Equal(1_500.00m, loan.CurrentBalance);
        Assert.Equal(LoanType.Personal, loan.Type);
        Assert.Equal("Maria Silva", loan.ApplicantName);
        Assert.Equal(LoanStatus.Active, loan.Status);
        Assert.Equal(CreatedAtUtc, loan.CreatedAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10.001)]
    public void CreateWithInvalidAmountShouldThrow(decimal amount)
    {
        var action = () => Loan.Create(Guid.NewGuid(), amount, LoanType.Personal, "Maria Silva", CreatedAtUtc);

        Assert.Throws<InvalidLoanException>(action);
    }

    [Fact]
    public void CreateWithMissingApplicantNameShouldThrow()
    {
        var action = () => Loan.Create(Guid.NewGuid(), 100m, LoanType.Personal, "  ", CreatedAtUtc);

        Assert.Throws<InvalidLoanException>(action);
    }

    [Fact]
    public void CreateWithMaximumSupportedAmountShouldSucceed()
    {
        var loan = Loan.Create(
            Guid.NewGuid(),
            Loan.MaximumAmount,
            LoanType.Personal,
            "Maria Silva",
            CreatedAtUtc);

        Assert.Equal(Loan.MaximumAmount, loan.Amount);
    }

    [Fact]
    public void CreateAboveMaximumSupportedAmountShouldThrow()
    {
        var action = () => Loan.Create(
            Guid.NewGuid(),
            Loan.MaximumAmount + 0.01m,
            LoanType.Personal,
            "Maria Silva",
            CreatedAtUtc);

        Assert.Throws<InvalidLoanException>(action);
    }

    [Fact]
    public void CreateWithEmptyIdentifierShouldThrow()
    {
        var action = () => Loan.Create(Guid.Empty, 100m, LoanType.Personal, "Maria Silva", CreatedAtUtc);

        Assert.Throws<InvalidLoanException>(action);
    }

    [Fact]
    public void CreateWithApplicantNameAboveMaximumLengthShouldThrow()
    {
        var applicantName = new string('a', Loan.ApplicantNameMaximumLength + 1);

        var action = () => Loan.Create(Guid.NewGuid(), 100m, LoanType.Personal, applicantName, CreatedAtUtc);

        Assert.Throws<InvalidLoanException>(action);
    }

    [Fact]
    public void ApplyPaymentWithPartialAmountShouldDeductBalanceAndRemainActive()
    {
        var loan = Loan.Create(Guid.NewGuid(), 1_500m, LoanType.Personal, "Maria Silva", CreatedAtUtc);

        loan.ApplyPayment(500m);

        Assert.Equal(1_000m, loan.CurrentBalance);
        Assert.Equal(LoanStatus.Active, loan.Status);
    }

    [Fact]
    public void ApplyPaymentWithRemainingBalanceShouldMarkLoanAsPaid()
    {
        var loan = Loan.Create(Guid.NewGuid(), 1_500m, LoanType.Personal, "Maria Silva", CreatedAtUtc);

        loan.ApplyPayment(1_500m);

        Assert.Equal(0m, loan.CurrentBalance);
        Assert.Equal(LoanStatus.Paid, loan.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1_500.01)]
    [InlineData(10.001)]
    public void ApplyPaymentWithInvalidAmountShouldThrow(decimal amount)
    {
        var loan = Loan.Create(Guid.NewGuid(), 1_500m, LoanType.Personal, "Maria Silva", CreatedAtUtc);

        var action = () => loan.ApplyPayment(amount);

        Assert.Throws<InvalidPaymentException>(action);
    }

    [Fact]
    public void ApplyPaymentWhenLoanIsPaidShouldThrow()
    {
        var loan = Loan.Create(Guid.NewGuid(), 1_500m, LoanType.Personal, "Maria Silva", CreatedAtUtc);
        loan.ApplyPayment(1_500m);

        var action = () => loan.ApplyPayment(1m);

        Assert.Throws<InvalidPaymentException>(action);
    }

    [Fact]
    public void ApplyPaymentAboveMaximumSupportedAmountShouldThrow()
    {
        var loan = Loan.Create(Guid.NewGuid(), Loan.MaximumAmount, LoanType.Personal, "Maria Silva", CreatedAtUtc);

        var action = () => loan.ApplyPayment(Loan.MaximumAmount + 0.01m);

        Assert.Throws<InvalidPaymentException>(action);
    }
}
