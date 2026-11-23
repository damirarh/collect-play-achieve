using CollectPlayAchieve.Api.Infrastructure;

namespace CollectPlayAchieve.Api.Tests.Infrastructure;

internal sealed record TestError : Error
{
    private TestError(
        ErrorType type,
        string code,
        string description,
        IReadOnlyDictionary<string, string[]>? validationErrors = null
    )
        : base(type, code, description, validationErrors) { }

    public static readonly TestError ValidationWithoutFieldErrors = new(
        ErrorType.Validation,
        "Invalid",
        "Invalid input."
    );

    public static readonly TestError Conflict = new(
        ErrorType.Conflict,
        "Conflict",
        "Already exists."
    );
}
