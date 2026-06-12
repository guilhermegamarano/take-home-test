using Fundo.Application.Common;
using Fundo.Domain.Loans;

namespace Fundo.Application.Loans;

public static class LoanCommandValidator
{
    public static void Validate(CreateLoanCommand command, LoanProductRule product)
    {
        var errors = new Dictionary<string, string[]>();

        if (command.Amount <= 0)
        {
            errors[nameof(command.Amount)] = ["Amount must be greater than zero."];
        }
        else if (command.Amount > Loan.MaximumAmount)
        {
            errors[nameof(command.Amount)] = [$"Amount cannot exceed {Loan.MaximumAmount:F2}."];
        }
        else if (decimal.Round(command.Amount, 2) != command.Amount)
        {
            errors[nameof(command.Amount)] = ["Amount cannot have more than two decimal places."];
        }
        else if (command.Amount < product.MinimumAmount || command.Amount > product.MaximumAmount)
        {
            errors[nameof(command.Amount)] =
            [
                $"Amount for {product.Type} loans must be between " +
                $"{product.MinimumAmount:F2} and {product.MaximumAmount:F2}.",
            ];
        }

        var applicantName = command.ApplicantName?.Trim();
        if (string.IsNullOrWhiteSpace(applicantName))
        {
            errors[nameof(command.ApplicantName)] = ["Applicant name is required."];
        }
        else if (applicantName.Length > Loan.ApplicantNameMaximumLength)
        {
            errors[nameof(command.ApplicantName)] =
                [$"Applicant name cannot exceed {Loan.ApplicantNameMaximumLength} characters."];
        }

        ThrowIfInvalid(errors);
    }

    public static void Validate(MakePaymentCommand command)
    {
        var errors = new Dictionary<string, string[]>();

        if (command.Amount <= 0)
        {
            errors[nameof(command.Amount)] = ["Amount must be greater than zero."];
        }
        else if (command.Amount > Loan.MaximumAmount)
        {
            errors[nameof(command.Amount)] = [$"Amount cannot exceed {Loan.MaximumAmount:F2}."];
        }
        else if (decimal.Round(command.Amount, 2) != command.Amount)
        {
            errors[nameof(command.Amount)] = ["Amount cannot have more than two decimal places."];
        }

        ThrowIfInvalid(errors);
    }

    private static void ThrowIfInvalid(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}
