using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;

namespace CollectPlayAchieve.Api.Infrastructure;

internal static class ValidationDecorator
{
    internal sealed class CommandBaseHandler<TCommand>(
        ICommandHandler<TCommand> innerHandler,
        IEnumerable<IValidator<TCommand>> validators
    ) : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public async Task<Result> Handle(TCommand command, CancellationToken cancellationToken)
        {
            var failures = await ValidateAsync(command, validators, cancellationToken);
            return failures.Length == 0
                ? await innerHandler.Handle(command, cancellationToken)
                : Result.Failure(CreateValidationError(failures));
        }
    }

    internal sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> innerHandler,
        IEnumerable<IValidator<TCommand>> validators
    ) : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public async Task<Result<TResponse>> Handle(
            TCommand command,
            CancellationToken cancellationToken
        )
        {
            var failures = await ValidateAsync(command, validators, cancellationToken);
            return failures.Length == 0
                ? await innerHandler.Handle(command, cancellationToken)
                : Result<TResponse>.Failure(CreateValidationError(failures));
        }
    }

    internal sealed class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> innerHandler,
        IEnumerable<IValidator<TQuery>> validators
    ) : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public async Task<Result<TResponse>> Handle(
            TQuery query,
            CancellationToken cancellationToken
        )
        {
            var failures = await ValidateAsync(query, validators, cancellationToken);
            return failures.Length == 0
                ? await innerHandler.Handle(query, cancellationToken)
                : Result<TResponse>.Failure(CreateValidationError(failures));
        }
    }

    private static async Task<ValidationFailure[]> ValidateAsync<TRequest>(
        TRequest request,
        IEnumerable<IValidator<TRequest>> validators,
        CancellationToken cancellationToken
    )
    {
        var results = await Task.WhenAll(
            validators.Select(validator =>
                validator.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken)
            )
        );
        return results.SelectMany(result => result.Errors).ToArray();
    }

    private static Error CreateValidationError(IEnumerable<ValidationFailure> failures) =>
        Error.Common.ValidationFailed(
            failures
                .GroupBy(failure => JsonNamingPolicy.CamelCase.ConvertName(failure.PropertyName))
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(failure => failure.ErrorMessage).ToArray()
                )
        );
}
