namespace CollectPlayAchieve.Api.Infrastructure;

internal class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None || !isSuccess && error == Error.None)
        {
            throw new ArgumentException("Invalid error", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);
}

internal class Result<T> : Result
{
    private readonly T _value;

    private Result(bool isSuccess, Error error, T value)
        : base(isSuccess, error)
    {
        _value = value;
    }

    public T Value =>
        !IsSuccess
            ? throw new InvalidOperationException("Cannot access the value of a failed result.")
            : _value;

    public static Result<T> Success(T value) => new(true, Error.None, value);

    public static new Result<T> Failure(Error error) => new(false, error, default!);
}
