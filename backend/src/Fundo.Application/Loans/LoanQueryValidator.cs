using Fundo.Application.Common;

namespace Fundo.Application.Loans;

public static class LoanQueryValidator
{
    public const int MaximumPageSize = 50;

    private static readonly string[] ValidStatuses = ["active", "paid"];
    private static readonly string[] ValidTypes = ["personal", "small-business", "bridge"];

    public static void Validate(LoanListQuery query)
    {
        var errors = new Dictionary<string, string[]>();

        if (query.Page < 1)
        {
            errors[nameof(query.Page)] = ["Page must be greater than or equal to 1."];
        }

        if (query.PageSize is < 1 or > MaximumPageSize)
        {
            errors[nameof(query.PageSize)] = [$"Page size must be between 1 and {MaximumPageSize}."];
        }

        if (!IsEmptyOrAllowed(query.Status, ValidStatuses))
        {
            errors[nameof(query.Status)] = ["Status must be either active or paid."];
        }

        if (!IsEmptyOrAllowed(query.Type, ValidTypes))
        {
            errors[nameof(query.Type)] = ["Type must be personal, small-business or bridge."];
        }

        if (query.ApplicantName?.Trim().Length > 100)
        {
            errors[nameof(query.ApplicantName)] = ["Applicant name filter cannot exceed 100 characters."];
        }

        if (query.MinimumBalance is < 0)
        {
            errors[nameof(query.MinimumBalance)] = ["Minimum balance cannot be negative."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    private static bool IsEmptyOrAllowed(string? value, string[] allowedValues) =>
        string.IsNullOrWhiteSpace(value) ||
        allowedValues.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);
}
