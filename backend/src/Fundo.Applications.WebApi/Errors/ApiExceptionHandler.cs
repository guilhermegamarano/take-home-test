using Fundo.Application.Abstractions;
using Fundo.Application.Common;
using Fundo.Domain.Loans;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Fundo.Applications.WebApi.Errors;

public sealed partial class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, errors) = exception switch
        {
            ValidationException validationException =>
                (StatusCodes.Status400BadRequest, "Validation failed", validationException.Errors),
            InvalidPaymentException =>
                (StatusCodes.Status409Conflict, "Payment cannot be applied", null),
            ConcurrencyConflictException =>
                (StatusCodes.Status409Conflict, "Concurrent update detected", null),
            LoanException =>
                (StatusCodes.Status422UnprocessableEntity, "Loan operation failed", null),
            _ =>
                (StatusCodes.Status500InternalServerError, "An unexpected error occurred", null),
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(logger, exception, httpContext.TraceIdentifier);
        }
        else
        {
            LogRequestFailure(logger, statusCode, httpContext.TraceIdentifier);
        }

        httpContext.Response.StatusCode = statusCode;
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = statusCode == StatusCodes.Status500InternalServerError ? null : exception.Message,
        };
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;
        if (errors is not null)
        {
            problemDetails.Extensions["errors"] = errors;
        }

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
        });
    }

    [LoggerMessage(LogLevel.Error, "Unhandled exception for trace {TraceId}")]
    private static partial void LogUnhandledException(
        ILogger logger,
        Exception exception,
        string traceId);

    [LoggerMessage(LogLevel.Warning, "Request failed with status {StatusCode} for trace {TraceId}")]
    private static partial void LogRequestFailure(ILogger logger, int statusCode, string traceId);
}
