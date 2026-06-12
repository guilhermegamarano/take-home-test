namespace Fundo.Application.Loans;

public sealed class LoanProductOptions
{
    public const string SectionName = "LoanProducts";

    public string DefaultType { get; init; } = "personal";

    public IReadOnlyList<LoanProductRule> Products { get; init; } = [];

    public void Validate()
    {
        var catalog = new LoanProductCatalog(DefaultType, Products);
        _ = catalog.Resolve(null);
    }
}
