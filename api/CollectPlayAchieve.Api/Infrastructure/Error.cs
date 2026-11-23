namespace CollectPlayAchieve.Api.Infrastructure;

internal record Error
{
    protected Error(
        ErrorType type,
        string code,
        string description,
        IReadOnlyDictionary<string, string[]>? validationErrors = null
    )
    {
        Type = type;
        Code = code;
        Description = description;
        ValidationErrors = validationErrors;
    }

    public ErrorType Type { get; }

    public string Code { get; }

    public string Description { get; }

    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; }

    public static readonly Error None = new(ErrorType.None, string.Empty, string.Empty);

    internal static class Common
    {
        public static readonly Error InternalServerError = new(
            ErrorType.InternalServerError,
            "Common.InternalServerError",
            "An unexpected error occurred."
        );

        public static Error ValidationFailed(
            IReadOnlyDictionary<string, string[]> validationErrors
        ) =>
            new(
                ErrorType.Validation,
                "Common.ValidationFailed",
                "One or more validation errors occurred.",
                validationErrors
            );
    }

    internal static class Registration
    {
        public static readonly Error UserAlreadyExists = new(
            ErrorType.Conflict,
            "Registration.UserAlreadyExists",
            "A user already exists. Registration is not available."
        );
    }
}
