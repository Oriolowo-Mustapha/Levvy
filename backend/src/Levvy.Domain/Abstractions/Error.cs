namespace Levvy.Domain.Abstractions;

/// <summary>
/// Represents a domain/application error with a code and description (dotnet-result-pattern).
/// </summary>
public sealed record Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("Error.NullValue", "A null value was provided.");

    public static implicit operator string(Error error) => error.Code;
    public override string ToString() => Code;
}
