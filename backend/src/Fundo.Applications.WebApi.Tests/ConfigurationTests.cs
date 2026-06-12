using Fundo.Application.Common;
using Fundo.Application.Loans;
using Fundo.Applications.WebApi.Authentication;
using Fundo.Domain.Loans;

namespace Fundo.Applications.WebApi.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void JwtOptionsWithValidValuesShouldPassValidation()
    {
        var options = CreateValidOptions();

        options.Validate();
    }

    [Theory]
    [InlineData("", "audience", "valid-signing-key-with-at-least-32-characters", "user", "password", 30)]
    [InlineData("issuer", "", "valid-signing-key-with-at-least-32-characters", "user", "password", 30)]
    [InlineData("issuer", "audience", "short", "user", "password", 30)]
    [InlineData("issuer", "audience", "valid-signing-key-with-at-least-32-characters", "", "password", 30)]
    [InlineData("issuer", "audience", "valid-signing-key-with-at-least-32-characters", "user", "", 30)]
    [InlineData("issuer", "audience", "valid-signing-key-with-at-least-32-characters", "user", "password", 0)]
    [InlineData("issuer", "audience", "valid-signing-key-with-at-least-32-characters", "user", "password", 121)]
    public void JwtOptionsWithInvalidValuesShouldFailValidation(
        string issuer,
        string audience,
        string signingKey,
        string username,
        string password,
        int tokenLifetimeMinutes)
    {
        var options = new JwtOptions
        {
            Issuer = issuer,
            Audience = audience,
            SigningKey = signingKey,
            Username = username,
            Password = password,
            TokenLifetimeMinutes = tokenLifetimeMinutes,
        };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void JwtOptionsShouldRejectPartiallyConfiguredReadOnlyCredentials()
    {
        var options = new JwtOptions
        {
            Issuer = "issuer",
            Audience = "audience",
            SigningKey = "valid-signing-key-with-at-least-32-characters",
            Username = "user",
            Password = "password",
            ReadOnlyUsername = "auditor",
            TokenLifetimeMinutes = 30,
        };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void CreateLoanValidationShouldReportAllInvalidFields()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            LoanCommandValidator.Validate(
                new CreateLoanCommand(Loan.MaximumAmount + 0.01m, new string('a', 201)),
                CreatePersonalProduct()));

        Assert.Contains(nameof(CreateLoanCommand.Amount), exception.Errors.Keys);
        Assert.Contains(nameof(CreateLoanCommand.ApplicantName), exception.Errors.Keys);
    }

    [Fact]
    public void LoanProductCatalogShouldResolveDefaultProduct()
    {
        var catalog = CreateCatalog();

        var product = catalog.Resolve(null);

        Assert.Equal("personal", product.Type);
        Assert.Equal(LoanType.Personal, catalog.ResolveLoanType(null));
    }

    [Fact]
    public void LoanProductCatalogShouldRejectUnsupportedProduct()
    {
        var catalog = CreateCatalog();

        var exception = Assert.Throws<ValidationException>(() => catalog.Resolve("unsupported"));

        Assert.Contains(nameof(CreateLoanCommand.Type), exception.Errors.Keys);
    }

    [Fact]
    public void LoanProductCatalogShouldRejectDisabledProductForNewLoans()
    {
        var catalog = new LoanProductCatalog(
            "personal",
            [CreatePersonalProduct(), new LoanProductRule("bridge", 50_000m, 1_000_000m, 500m, false)]);

        var exception = Assert.Throws<ValidationException>(() => catalog.Resolve("bridge"));

        Assert.Contains(nameof(CreateLoanCommand.Type), exception.Errors.Keys);
    }

    [Fact]
    public void CreateLoanValidationShouldApplyProductLimits()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            LoanCommandValidator.Validate(new CreateLoanCommand(499.99m, "Maria Silva"), CreatePersonalProduct()));

        Assert.Contains(nameof(CreateLoanCommand.Amount), exception.Errors.Keys);
    }

    [Fact]
    public void PaymentValidationShouldRejectTooManyDecimalPlaces()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            LoanCommandValidator.Validate(new MakePaymentCommand(10.001m)));

        Assert.Contains(nameof(MakePaymentCommand.Amount), exception.Errors.Keys);
    }

    [Fact]
    public void LoanProductCatalogShouldAllowFinalPaymentBelowMinimum()
    {
        var catalog = CreateCatalog();
        var loan = Loan.Create(
            Guid.NewGuid(),
            500m,
            LoanType.Personal,
            "Maria Silva",
            new DateTimeOffset(2026, 6, 11, 12, 0, 0, TimeSpan.Zero));

        catalog.ValidatePayment(loan, 500m);
    }

    [Fact]
    public void LoanProductCatalogShouldRejectPartialPaymentBelowMinimum()
    {
        var catalog = CreateCatalog();
        var loan = Loan.Create(
            Guid.NewGuid(),
            500m,
            LoanType.Personal,
            "Maria Silva",
            new DateTimeOffset(2026, 6, 11, 12, 0, 0, TimeSpan.Zero));

        var exception = Assert.Throws<ValidationException>(() => catalog.ValidatePayment(loan, 10m));

        Assert.Contains(nameof(MakePaymentCommand.Amount), exception.Errors.Keys);
    }

    private static JwtOptions CreateValidOptions() =>
        new()
        {
            Issuer = "issuer",
            Audience = "audience",
            SigningKey = "valid-signing-key-with-at-least-32-characters",
            Username = "user",
            Password = "password",
            TokenLifetimeMinutes = 30,
        };

    private static LoanProductRule CreatePersonalProduct() =>
        new("personal", 500m, 25_000m, 25m, true);

    private static LoanProductCatalog CreateCatalog() =>
        new(
            "personal",
            [
                CreatePersonalProduct(),
                new LoanProductRule("small-business", 10_000m, 250_000m, 100m, true),
                new LoanProductRule("bridge", 50_000m, 1_000_000m, 500m, true),
            ]);
}
