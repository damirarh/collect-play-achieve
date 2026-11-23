using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CollectPlayAchieve.Api.Infrastructure;

internal sealed partial class UnhandledExceptionHandler(ILogger<UnhandledExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        var traceId = httpContext.TraceIdentifier;
        LogUnhandledException(logger, traceId, exception);

        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            Title = Error.Common.InternalServerError.Code,
            Detail = Error.Common.InternalServerError.Description,
            Status = StatusCodes.Status500InternalServerError,
            Extensions = { ["traceId"] = traceId },
        };

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken
        );
        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for request {TraceId}")]
    private static partial void LogUnhandledException(
        ILogger logger,
        string traceId,
        Exception exception
    );
}
