using Fundo.Application.Common;
using Fundo.Domain.Loans;

namespace Fundo.Application.Loans;

public sealed class LoanProductCatalog : ILoanProductCatalog
{
    private readonly string defaultType;
    private readonly IReadOnlyDictionary<string, LoanProductRule> products;

    public LoanProductCatalog(string defaultType, IEnumerable<LoanProductRule> products)
    {
        this.defaultType = Normalize(defaultType);
        this.products = products.ToDictionary(product => Normalize(product.Type), StringComparer.Ordinal);

        if (!this.products.ContainsKey(this.defaultType))
        {
            throw new InvalidOperationException("Default loan product type must be configured.");
        }

        foreach (var product in this.products.Values)
        {
            if (product.MinimumAmount <= 0 ||
                product.MaximumAmount <= 0 ||
                product.MaximumAmount > Loan.MaximumAmount ||
                product.MinimumAmount > product.MaximumAmount ||
                product.MinimumPayment <= 0)
            {
                throw new InvalidOperationException(
                    $"Loan product '{product.Type}' has invalid monetary limits.");
            }
        }
    }

    public LoanProductRule Resolve(string? type)
    {
        var normalizedType = Normalize(string.IsNullOrWhiteSpace(type) ? defaultType : type);
        if (!products.TryGetValue(normalizedType, out var product))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(CreateLoanCommand.Type)] = [$"Loan product '{type}' is not supported."],
            });
        }

        if (!product.Enabled)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(CreateLoanCommand.Type)] = [$"Loan product '{type}' is currently disabled."],
            });
        }

        return product;
    }

    public LoanType ResolveLoanType(string? type) =>
        Normalize(Resolve(type).Type) switch
        {
            "personal" => LoanType.Personal,
            "small-business" => LoanType.SmallBusiness,
            "bridge" => LoanType.Bridge,
            _ => throw new InvalidOperationException("Loan product type is not mapped to a domain type."),
        };

    public void ValidatePayment(Loan loan, decimal paymentAmount)
    {
        if (loan.Status == LoanStatus.Paid)
        {
            return;
        }

        var product = GetProduct(FormatType(loan.Type));
        if (paymentAmount < product.MinimumPayment && paymentAmount != loan.CurrentBalance)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(MakePaymentCommand.Amount)] =
                [
                    $"Payment amount must be at least {product.MinimumPayment:F2} " +
                    "unless it pays the remaining balance.",
                ],
            });
        }
    }

    private LoanProductRule GetProduct(string type)
    {
        var normalizedType = Normalize(type);
        if (!products.TryGetValue(normalizedType, out var product))
        {
            throw new InvalidOperationException($"Loan product '{type}' is not configured.");
        }

        return product;
    }

    private static string FormatType(LoanType type) =>
        type switch
        {
            LoanType.Personal => "personal",
            LoanType.SmallBusiness => "small-business",
            LoanType.Bridge => "bridge",
            _ => throw new InvalidOperationException("Unsupported loan type."),
        };

    private static string Normalize(string? type) =>
        type?.Trim().ToLowerInvariant() ?? string.Empty;
}
