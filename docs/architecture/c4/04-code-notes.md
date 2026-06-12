# API Code Notes

## Main Flow

`LoansController` receives explicit request records and delegates to
`LoanService`. The service resolves the configured loan product, performs
native request validation, creates or loads the `Loan` aggregate, invokes its
business behavior and commits through `IUnitOfWork`. Infrastructure implements
the ports with EF Core and SQL Server.

The Angular client uses all loan endpoints: it lists and filters the portfolio,
pages through results, fetches a loan detail by id, creates new loans and
applies payments. Browser authentication uses `/auth/session` and an HttpOnly
SameSite cookie, while `/auth/token` remains available for API clients that
need bearer tokens.

The frontend runtime proxy is configured through `API_UPSTREAM`. Docker Compose
sets it to the local API service name, while the Azure deployment sets it to
the internal API Container App URL. This keeps browser traffic same-origin
without baking environment-specific endpoints into the Angular bundle.

## Business Rules

- A loan starts active with its full amount as the current balance.
- New loans are created for one configured product: `personal`,
  `small-business` or `bridge`.
- Product rules are parameterized in `LoanProducts`: minimum amount, maximum
  amount, minimum partial payment and an `enabled` switch.
- Disabling a product blocks new originations but does not block payments for
  existing loans of that product.
- Amounts must be positive and contain at most two decimal places.
- Amounts must fit the selected product range.
- Payments cannot exceed the current balance.
- Partial payments must meet the product minimum payment; a final payoff may be
  below that minimum.
- A zero balance transitions the aggregate to `paid`.
- A paid loan cannot receive another payment.
- Amounts are capped at the maximum value supported by SQL Server
  `decimal(18,2)`, so API validation fails with 400 instead of leaking database
  precision failures as 500 errors.
- Cookie-authenticated write requests require `X-Requested-With:
  XMLHttpRequest`, added by the Angular interceptor. Bearer-token API clients
  are not coupled to this browser-only protection.
- Read endpoints require the `loans.read` permission. Write endpoints require
  `loans.write`.
- List queries are filtered and paged in SQL through `IQueryable`, with invalid
  query values rejected before persistence.

## Extension Points

- Add idempotency before invoking `MakePaymentAsync`.
- Add new loan products by extending `LoanType`, `LoanProductCatalog` mapping,
  `LoanProducts` configuration and the EF check constraint/migration.
- Replace the local token endpoint with OpenID Connect configuration.
- Split a dedicated BFF only when the system grows beyond this same-origin
  assessment deployment and needs server-side token exchange or per-user
  authorization policies.
- Export native activities and metrics through an OTLP-compatible provider.
- Promote the Azure deployment reference to production by adding private SQL
  networking and a dedicated migration job for each schema-changing release.
