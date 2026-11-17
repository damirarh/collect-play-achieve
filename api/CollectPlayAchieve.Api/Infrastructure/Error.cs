namespace CollectPlayAchieve.Api.Infrastructure;

internal sealed record Error(
    ErrorType Type,
    string Code,
    string Description,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null
)
{
    public static readonly Error None = new(ErrorType.None, string.Empty, string.Empty);
}
