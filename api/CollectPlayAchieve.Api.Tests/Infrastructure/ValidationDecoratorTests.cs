using CollectPlayAchieve.Api.Infrastructure;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CollectPlayAchieve.Api.Tests.Infrastructure;

internal sealed class ValidationDecoratorTests
{
    [Test]
    [MethodDataSource(nameof(HandleMethodTestData))]
    public async Task InvalidRequest_AggregatesFieldErrors_WithoutInvokingHandler(
        Func<IServiceProvider, TestRequest, CancellationToken, Task<Result>> handleFunc
    )
    {
        var first = new InlineValidator<TestRequest>();
        first.RuleFor(request => request.Username).NotNull().WithMessage("Username is required.");
        first
            .RuleFor(request => request.Username)
            .NotEmpty()
            .WithMessage("Username must not be empty.");
        var second = new InlineValidator<TestRequest>();
        second.RuleFor(request => request.Username).NotNull().WithMessage("Username rejected.");
        second.RuleFor(request => request.Password).NotNull().WithMessage("Password is required.");
        var handler = new TestHandler();
        await using var services = CreateServices(handler, first, second);

        var result = await handleFunc(
            services,
            new TestRequest(null, null),
            CancellationToken.None
        );

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Type).IsEqualTo(ErrorType.Validation);
        await Assert.That(result.Error.Code).IsEqualTo("ValidationError");
        await Assert
            .That(result.Error.Description)
            .IsEqualTo("One or more validation errors occurred.");
        var errors = result.Error.ValidationErrors!;
        await Assert.That(errors.Count).IsEqualTo(2);
        await Assert.That(errors["username"].Length).IsEqualTo(3);
        await Assert
            .That(errors["username"])
            .IsEquivalentTo([
                "Username is required.",
                "Username must not be empty.",
                "Username rejected.",
            ]);
        await Assert.That(errors["password"]).IsEquivalentTo(["Password is required."]);
        await Assert.That(handler.Calls).IsEqualTo(0);
    }

    [Test]
    [MatrixDataSource]
    public async Task ValidRequest_WithOrWithoutValidators_PreservesInnerResultAndCancellation(
        [MatrixMethod<ValidationDecoratorTests>(nameof(HandleMethodMatrixData))]
            Func<IServiceProvider, TestRequest, CancellationToken, Task<Result>> handleFunc,
        [Matrix(false, true)] bool withValidator,
        [Matrix(false, true)] bool innerFailure
    )
    {
        using var cancellation = new CancellationTokenSource();
        var request = new TestRequest("admin", "password");
        var handler = new TestHandler(innerFailure);
        var validator = new InlineValidator<TestRequest>();
        CancellationToken? validationToken = null;
        validator
            .RuleFor(value => value.Username)
            .MustAsync(
                (_, token) =>
                {
                    validationToken = token;
                    return Task.FromResult(true);
                }
            );
        await using var services = CreateServices(handler, withValidator ? [validator] : []);

        var result = await handleFunc(services, request, cancellation.Token);

        await Assert.That(result).IsSameReferenceAs(handler.Result);
        await Assert.That(handler.Calls).IsEqualTo(1);
        await Assert.That(handler.Request).IsSameReferenceAs(request);
        await Assert.That(handler.Token).IsEqualTo(cancellation.Token);
        if (withValidator)
        {
            await Assert.That(validationToken).IsEqualTo(cancellation.Token);
        }
    }

    [Test]
    [MethodDataSource(nameof(HandleMethodTestData))]
    public async Task ValidatorException_PropagatesWithoutInvokingHandler(
        Func<IServiceProvider, TestRequest, CancellationToken, Task<Result>> handleFunc
    )
    {
        var handler = new TestHandler();
        var validator = new InlineValidator<TestRequest>();
        validator
            .RuleFor(request => request.Username)
            .MustAsync((_, _) => throw new InvalidOperationException("Validator failed."));
        await using var services = CreateServices(handler, validator);

        await AssertValidatorException(services, handleFunc);
        await Assert.That(handler.Calls).IsEqualTo(0);
        return;

        static async Task AssertValidatorException(
            IServiceProvider services,
            Func<IServiceProvider, TestRequest, CancellationToken, Task<Result>> handleFunc
        )
        {
            await Assert
                .That(async () =>
                {
                    await handleFunc(
                        services,
                        new TestRequest("admin", "password"),
                        CancellationToken.None
                    );
                })
                .Throws<InvalidOperationException>()
                .WithMessage("Validator failed.");
        }
    }

    [Test]
    [MethodDataSource(nameof(HandleMethodTestData))]
    public async Task CancelledValidation_PropagatesWithoutInvokingHandler(
        Func<IServiceProvider, TestRequest, CancellationToken, Task<Result>> handleFunc
    )
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var handler = new TestHandler();
        var validator = new InlineValidator<TestRequest>();
        validator.RuleFor(request => request.Username).NotNull();
        await using var services = CreateServices(handler, validator);

        await AssertCancelledValidation(services, cancellation.Token, handleFunc);
        await Assert.That(handler.Calls).IsEqualTo(0);
        return;

        static async Task AssertCancelledValidation(
            IServiceProvider services,
            CancellationToken cancellationToken,
            Func<IServiceProvider, TestRequest, CancellationToken, Task<Result>> handleFunc
        )
        {
            await Assert
                .That(async () =>
                {
                    await handleFunc(
                        services,
                        new TestRequest("admin", "password"),
                        cancellationToken
                    );
                })
                .Throws<OperationCanceledException>();
        }
    }

    private static ServiceProvider CreateServices(
        TestHandler handler,
        params IValidator<TestRequest>[] validators
    )
    {
        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<TestRequest>>(_ => handler);
        services.AddScoped<ICommandHandler<TestRequest, string>>(_ => handler);
        services.AddScoped<IQueryHandler<TestRequest, string>>(_ => handler);
        foreach (var validator in validators)
        {
            services.AddScoped<IValidator<TestRequest>>(_ => validator);
        }

        services.Decorate(
            typeof(ICommandHandler<>),
            typeof(ValidationDecorator.CommandBaseHandler<>)
        );
        services.Decorate(
            typeof(ICommandHandler<,>),
            typeof(ValidationDecorator.CommandHandler<,>)
        );
        services.Decorate(typeof(IQueryHandler<,>), typeof(ValidationDecorator.QueryHandler<,>));
        return services.BuildServiceProvider();
    }

    public static IEnumerable<
        Func<Func<IServiceProvider, TestRequest, CancellationToken, Task<Result>>>
    > HandleMethodTestData()
    {
        yield return () =>
            async (services, request, token) =>
                await services
                    .GetRequiredService<ICommandHandler<TestRequest>>()
                    .Handle(request, token);
        yield return () =>
            async (services, request, token) =>
                await services
                    .GetRequiredService<ICommandHandler<TestRequest, string>>()
                    .Handle(request, token);
        yield return () =>
            async (services, request, token) =>
                await services
                    .GetRequiredService<IQueryHandler<TestRequest, string>>()
                    .Handle(request, token);
    }

    public static IEnumerable<
        Func<IServiceProvider, TestRequest, CancellationToken, Task<Result>>
    > HandleMethodMatrixData()
    {
        return HandleMethodTestData().Select(factory => factory());
    }

    public sealed record TestRequest(string? Username, string? Password)
        : ICommand,
            ICommand<string>,
            IQuery<string>;

    private sealed class TestHandler(bool fail = false)
        : ICommandHandler<TestRequest>,
            ICommandHandler<TestRequest, string>,
            IQueryHandler<TestRequest, string>
    {
        public Result<string> Result { get; } =
            fail
                ? Result<string>.Failure(
                    new Error(ErrorType.Conflict, "Conflict", "Already handled.")
                )
                : Result<string>.Success("Handled");

        public int Calls { get; private set; }

        public TestRequest? Request { get; private set; }

        public CancellationToken Token { get; private set; }

        public Task<Result<string>> Handle(TestRequest command, CancellationToken cancellationToken)
        {
            Calls++;
            Request = command;
            Token = cancellationToken;
            return Task.FromResult(Result);
        }

        async Task<Result> ICommandHandler<TestRequest>.Handle(
            TestRequest command,
            CancellationToken cancellationToken
        ) => await Handle(command, cancellationToken);
    }
}
