namespace Fundo.Application.Abstractions;

public sealed class ConcurrencyConflictException(string message, Exception innerException)
    : Exception(message, innerException);
