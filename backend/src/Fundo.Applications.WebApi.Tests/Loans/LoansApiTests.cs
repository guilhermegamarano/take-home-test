using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fundo.Application.Loans;
using Fundo.Applications.WebApi.Authentication;
using Fundo.Applications.WebApi.Tests.Infrastructure;
using Fundo.Domain.Loans;

namespace Fundo.Applications.WebApi.Tests.Loans;

public sealed class LoansApiTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory factory;

    public LoansApiTests(TestApiFactory factory)
    {
        this.factory = factory;
        this.factory.Store.Reset();
    }

    [Fact]
    public async Task ListWithoutTokenShouldReturnUnauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/loans", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginWithValidCredentialsShouldReturnToken()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/token",
            new LoginRequest(TestApiFactory.Username, TestApiFactory.Password),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(
            CancellationToken.None);
        Assert.NotNull(token);
        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));
    }

    [Fact]
    public async Task SessionLoginWithValidCredentialsShouldSetHttpOnlyCookie()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/session",
            new LoginRequest(TestApiFactory.Username, TestApiFactory.Password),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value =>
            value.Contains("fundo.session=", StringComparison.Ordinal) &&
            value.Contains("path=/", StringComparison.OrdinalIgnoreCase) &&
            value.Contains("httponly", StringComparison.OrdinalIgnoreCase) &&
            value.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ListWithSessionCookieShouldReturnSeedLoan()
    {
        using var client = factory.CreateClient();
        await CreateSessionAsync(client);

        var loans = await client.GetFromJsonAsync<PagedResult<LoanResponse>>(
            "/loans",
            CancellationToken.None);

        Assert.NotNull(loans);
        Assert.Contains(loans.Items, loan => loan.ApplicantName == "John Doe");
        Assert.Equal(1, loans.Page);
        Assert.Equal(10, loans.PageSize);
        Assert.Equal(2, loans.TotalItems);
    }

    [Fact]
    public async Task CookieAuthenticatedWriteWithoutApplicationHeaderShouldReturnForbidden()
    {
        using var client = factory.CreateClient();
        await CreateSessionAsync(client);

        var response = await client.PostAsJsonAsync(
            "/loans",
            new CreateLoanCommand(1_500m, "Maria Silva"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CookieAuthenticatedWriteWithApplicationHeaderShouldCreateLoan()
    {
        using var client = factory.CreateClient();
        await CreateSessionAsync(client);
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");

        var response = await client.PostAsJsonAsync(
            "/loans",
            new CreateLoanCommand(1_500m, "Maria Silva"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task ReadOnlyUserShouldListAndGetLoans()
    {
        using var client = await CreateAuthenticatedClientAsync(
            TestApiFactory.ReadOnlyUsername,
            TestApiFactory.ReadOnlyPassword);

        var listResponse = await client.GetAsync("/loans", CancellationToken.None);
        var getResponse = await client.GetAsync(
            $"/loans/{TestApiFactory.ActiveSeedLoanId}",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task ReadOnlyUserShouldNotCreateOrPayLoans()
    {
        using var client = await CreateAuthenticatedClientAsync(
            TestApiFactory.ReadOnlyUsername,
            TestApiFactory.ReadOnlyPassword);

        var createResponse = await client.PostAsJsonAsync(
            "/loans",
            new CreateLoanCommand(1_500m, "Read Only"),
            CancellationToken.None);
        var paymentResponse = await client.PostAsJsonAsync(
            $"/loans/{TestApiFactory.ActiveSeedLoanId}/payment",
            new MakePaymentCommand(100m),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, paymentResponse.StatusCode);
    }

    [Fact]
    public async Task LoginWithMissingCredentialsShouldReturnValidationProblem()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/token",
            new LoginRequest("", ""),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task LoginWithInvalidCredentialsShouldReturnUnauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/auth/token",
            new LoginRequest(TestApiFactory.Username, "wrong-password"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListWithTokenShouldReturnSeedLoan()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var loans = await client.GetFromJsonAsync<PagedResult<LoanResponse>>(
            "/loans",
            CancellationToken.None);

        Assert.NotNull(loans);
        Assert.Contains(loans.Items, loan => loan.ApplicantName == "John Doe");
    }

    [Fact]
    public async Task ListWithFiltersShouldReturnPagedFilteredLoans()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var loans = await client.GetFromJsonAsync<PagedResult<LoanResponse>>(
            "/loans?page=1&pageSize=1&status=active&type=personal&applicantName=John",
            CancellationToken.None);

        Assert.NotNull(loans);
        Assert.Single(loans.Items);
        Assert.Equal("John Doe", loans.Items[0].ApplicantName);
        Assert.Equal(1, loans.Page);
        Assert.Equal(1, loans.PageSize);
        Assert.Equal(1, loans.TotalItems);
        Assert.Equal(1, loans.TotalPages);
    }

    [Fact]
    public async Task ListWithInvalidQueryShouldReturnValidationProblem()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(
            "/loans?page=0&pageSize=100&type=unsupported",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CreateWithValidRequestShouldReturnCreatedLoan()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            "/loans",
            new CreateLoanCommand(1_500m, "Maria Silva"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var loan = await response.Content.ReadFromJsonAsync<LoanResponse>(
            CancellationToken.None);
        Assert.NotNull(loan);
        Assert.Equal(loan.Amount, loan.CurrentBalance);
        Assert.Equal("personal", loan.Type);
        Assert.Equal("active", loan.Status);
    }

    [Fact]
    public async Task CreateWithSmallBusinessProductShouldReturnTypedLoan()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            "/loans",
            new CreateLoanCommand(10_000m, "Corner Market", "small-business"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var loan = await response.Content.ReadFromJsonAsync<LoanResponse>(
            CancellationToken.None);
        Assert.NotNull(loan);
        Assert.Equal("small-business", loan.Type);
    }

    [Fact]
    public async Task GetExistingLoanShouldReturnLoan()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync(
            $"/loans/{TestApiFactory.ActiveSeedLoanId}",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var loan = await response.Content.ReadFromJsonAsync<LoanResponse>(
            CancellationToken.None);
        Assert.NotNull(loan);
        Assert.Equal(TestApiFactory.ActiveSeedLoanId, loan.Id);
    }

    [Fact]
    public async Task GetMissingLoanShouldReturnNotFound()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/loans/{Guid.NewGuid()}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateWithInvalidRequestShouldReturnValidationProblem()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            "/loans",
            new CreateLoanCommand(0m, ""),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CreateAboveDatabasePrecisionShouldReturnValidationProblem()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            "/loans",
            new CreateLoanCommand(Loan.MaximumAmount + 0.01m, "Boundary Test"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CreateBelowProductMinimumShouldReturnValidationProblem()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            "/loans",
            new CreateLoanCommand(499.99m, "Small Request", "personal"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CreateWithUnsupportedProductShouldReturnValidationProblem()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            "/loans",
            new CreateLoanCommand(1_500m, "Unsupported Product", "payday"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PaymentWithRemainingBalanceShouldMarkLoanAsPaid()
    {
        using var client = await CreateAuthenticatedClientAsync();
        var createResponse = await client.PostAsJsonAsync(
            "/loans",
            new CreateLoanCommand(500m, "Payment Test"),
            CancellationToken.None);
        var created = await createResponse.Content.ReadFromJsonAsync<LoanResponse>(
            CancellationToken.None);
        Assert.NotNull(created);

        var paymentResponse = await client.PostAsJsonAsync(
            $"/loans/{created.Id}/payment",
            new MakePaymentCommand(500m),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, paymentResponse.StatusCode);
        var paidLoan = await paymentResponse.Content.ReadFromJsonAsync<LoanResponse>(
            CancellationToken.None);
        Assert.NotNull(paidLoan);
        Assert.Equal(0m, paidLoan.CurrentBalance);
        Assert.Equal("paid", paidLoan.Status);
    }

    [Fact]
    public async Task PaymentForMissingLoanShouldReturnNotFound()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/loans/{Guid.NewGuid()}/payment",
            new MakePaymentCommand(10m),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PartialPaymentBelowProductMinimumShouldReturnValidationProblem()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/loans/{TestApiFactory.ActiveSeedLoanId}/payment",
            new MakePaymentCommand(10m),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PaymentAboveCurrentBalanceShouldReturnConflictProblem()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/loans/{TestApiFactory.ActiveSeedLoanId}/payment",
            new MakePaymentCommand(20_000m),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PaymentForPaidLoanShouldReturnConflictProblem()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync(
            $"/loans/{TestApiFactory.PaidSeedLoanId}/payment",
            new MakePaymentCommand(1m),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private Task<HttpClient> CreateAuthenticatedClientAsync() =>
        CreateAuthenticatedClientAsync(TestApiFactory.Username, TestApiFactory.Password);

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string username, string password)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/auth/token",
            new LoginRequest(username, password),
            CancellationToken.None);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(
            CancellationToken.None);
        Assert.NotNull(token);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(token.TokenType, token.AccessToken);
        return client;
    }

    private static async Task CreateSessionAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/auth/session",
            new LoginRequest(TestApiFactory.Username, TestApiFactory.Password),
            CancellationToken.None);
        response.EnsureSuccessStatusCode();
    }
}
