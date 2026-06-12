namespace Fundo.Domain.Loans;

public sealed class Loan
{
    public const int ApplicantNameMaximumLength = 200;
    public const decimal MaximumAmount = 9_999_999_999_999_999.99m;

    private Loan()
    {
        ApplicantName = string.Empty;
    }

    private Loan(
        Guid id,
        decimal amount,
        LoanType type,
        string applicantName,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Amount = amount;
        CurrentBalance = amount;
        Type = type;
        ApplicantName = applicantName;
        Status = LoanStatus.Active;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public decimal Amount { get; private set; }

    public decimal CurrentBalance { get; private set; }

    public LoanType Type { get; private set; }

    public string ApplicantName { get; private set; }

    public LoanStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Loan Create(
        Guid id,
        decimal amount,
        LoanType type,
        string applicantName,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new InvalidLoanException("Loan identifier is required.");
        }

        if (amount <= 0)
        {
            throw new InvalidLoanException("Loan amount must be greater than zero.");
        }

        if (amount > MaximumAmount)
        {
            throw new InvalidLoanException($"Loan amount cannot exceed {MaximumAmount:F2}.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new InvalidLoanException("Loan amount cannot have more than two decimal places.");
        }

        var normalizedApplicantName = applicantName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedApplicantName))
        {
            throw new InvalidLoanException("Applicant name is required.");
        }

        if (normalizedApplicantName.Length > ApplicantNameMaximumLength)
        {
            throw new InvalidLoanException(
                $"Applicant name cannot exceed {ApplicantNameMaximumLength} characters.");
        }

        return new Loan(id, amount, type, normalizedApplicantName, createdAtUtc.ToUniversalTime());
    }

    public void ApplyPayment(decimal amount)
    {
        if (Status == LoanStatus.Paid)
        {
            throw new InvalidPaymentException("The loan has already been paid.");
        }

        if (amount <= 0)
        {
            throw new InvalidPaymentException("Payment amount must be greater than zero.");
        }

        if (amount > MaximumAmount)
        {
            throw new InvalidPaymentException($"Payment amount cannot exceed {MaximumAmount:F2}.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new InvalidPaymentException("Payment amount cannot have more than two decimal places.");
        }

        if (amount > CurrentBalance)
        {
            throw new InvalidPaymentException("Payment amount cannot exceed the current balance.");
        }

        CurrentBalance -= amount;
        if (CurrentBalance == 0)
        {
            Status = LoanStatus.Paid;
        }
    }
}
