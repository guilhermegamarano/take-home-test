using Fundo.Application.Loans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Fundo.Applications.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("loans")]
public sealed partial class LoansController(LoanService loanService, ILogger<LoansController> logger)
    : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "Loans.Write")]
    [EnableRateLimiting("writes")]
    [ProducesResponseType<LoanResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoanResponse>> Create(
        CreateLoanCommand command,
        CancellationToken cancellationToken)
    {
        var loan = await loanService.CreateAsync(command, cancellationToken);
        LogLoanCreated(logger, loan.Id);
        return CreatedAtAction(nameof(GetById), new { id = loan.Id }, loan);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Loans.Read")]
    [ProducesResponseType<LoanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoanResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var loan = await loanService.GetByIdAsync(id, cancellationToken);
        return loan is null ? NotFound() : Ok(loan);
    }

    [HttpGet]
    [Authorize(Policy = "Loans.Read")]
    [ProducesResponseType<PagedResult<LoanResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<LoanResponse>>> List(
        [FromQuery] LoanListQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await loanService.ListAsync(query, cancellationToken));
    }

    [HttpPost("{id:guid}/payment")]
    [Authorize(Policy = "Loans.Write")]
    [EnableRateLimiting("writes")]
    [ProducesResponseType<LoanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoanResponse>> MakePayment(
        Guid id,
        MakePaymentCommand command,
        CancellationToken cancellationToken)
    {
        var loan = await loanService.MakePaymentAsync(id, command, cancellationToken);
        if (loan is null)
        {
            return NotFound();
        }

        LogPaymentApplied(logger, loan.Id);
        return Ok(loan);
    }

    [LoggerMessage(LogLevel.Information, "Loan {LoanId} was created")]
    private static partial void LogLoanCreated(ILogger logger, Guid loanId);

    [LoggerMessage(LogLevel.Information, "Payment applied to loan {LoanId}")]
    private static partial void LogPaymentApplied(ILogger logger, Guid loanId);
}
