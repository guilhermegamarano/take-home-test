namespace Fundo.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
