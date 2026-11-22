using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using CollectPlayAchieve.Api.Features.Auth;
using CollectPlayAchieve.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CollectPlayAchieve.Api.Tests.Infrastructure;

internal sealed class UnhandledExceptionHandlerTests : ApiTestBase
{
    private const string Secret = "private-exception-detail-8675309";

    [Test]
    [Arguments("Development", false)]
    [Arguments("Development", true)]
    [Arguments("Production", false)]
    [Arguments("Production", true)]
    public async Task UnhandledException_ReturnsGenericProblem_AndLogsException(
        string environment,
        bool post
    )
    {
        var logs = new CapturingLoggerProvider();
        await using var factory = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
            builder.ConfigureTestServices(services =>
            {
                services.Replace(
                    ServiceDescriptor.Scoped<IQueryHandler<InitStatusQuery, InitStatusResponse>>(
                        _ => new FailingQueryHandler()
                    )
                );
                services.Replace(
                    ServiceDescriptor.Scoped<ICommandHandler<RegisterFirstUserCommand>>(
                        _ => new FailingCommandHandler()
                    )
                );
            });
        });
        using var client = factory.CreateClient();

        using var response = post
            ? await client.PostAsJsonAsync(
                "/init/register",
                new { username = "admin", password = "test" }
            )
            : await client.GetAsync("/init/status");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.InternalServerError);
        await Assert
            .That(response.Content.Headers.ContentType?.MediaType)
            .IsEqualTo("application/problem+json");
        await Assert
            .That(response)
            .MatchesProblemDetails(
                (int)HttpStatusCode.InternalServerError,
                title: "Common.InternalServerError",
                detail: "An unexpected error occurred.",
                type: "https://tools.ietf.org/html/rfc9110#section-15.6.1"
            );

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var problem = document.RootElement;
        var traceId = problem.GetProperty("traceId").GetString();

        await Assert
            .That(problem.EnumerateObject().Select(p => p.Name))
            .IsEquivalentTo(["status", "title", "detail", "type", "traceId"]);
        await Assert.That(string.IsNullOrWhiteSpace(traceId)).IsFalse();
        await Assert.That(body).DoesNotContain(Secret, StringComparison.Ordinal);
        await Assert
            .That(body)
            .DoesNotContain(nameof(PrivateTestException), StringComparison.Ordinal);
        await Assert
            .That(logs.Entries)
            .Any(entry =>
                entry.Exception is PrivateTestException
                && entry.Message.Contains(traceId!, StringComparison.Ordinal)
            );
    }

    private sealed class FailingQueryHandler : IQueryHandler<InitStatusQuery, InitStatusResponse>
    {
        public Task<Result<InitStatusResponse>> Handle(
            InitStatusQuery query,
            CancellationToken cancellationToken
        ) => throw new PrivateTestException();
    }

    private sealed class FailingCommandHandler : ICommandHandler<RegisterFirstUserCommand>
    {
        public Task<Result> Handle(
            RegisterFirstUserCommand command,
            CancellationToken cancellationToken
        ) => throw new PrivateTestException();
    }

    private sealed class PrivateTestException() : Exception(Secret);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

        public void Dispose() { }
    }

    private sealed record LogEntry(string Message, Exception? Exception);

    private sealed class CapturingLogger(ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => entries.Enqueue(new LogEntry(formatter(state, exception), exception));
    }
}
