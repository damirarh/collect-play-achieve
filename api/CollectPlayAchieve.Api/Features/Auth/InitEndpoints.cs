using System.ComponentModel;
using CollectPlayAchieve.Api.Infrastructure;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CollectPlayAchieve.Api.Features.Auth;

[UsedImplicitly]
internal sealed class InitEndpoints : IEndpoint
{
    private const string InitTagName = "Init";

    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder
            .MapGet("/init/status", GetInitStatus)
            .WithTags(InitTagName)
            .WithSummary("Get initialization status")
            .WithDescription(
                "Indicates whether registration is available because no user exists. "
                    + "Unexpected errors return a problem containing a traceId."
            )
            .Produces<InitStatusResponse>()
            .ProducesProblem(StatusCodes.Status500InternalServerError);
        builder
            .MapPost("/init/register", RegisterFirstUser)
            .WithTags(InitTagName)
            .WithSummary("Register the first user")
            .WithDescription(
                "Creates the first user as an administrator. Invalid credentials produce field "
                    + "validation errors; registration is unavailable once a user exists. "
                    + "Unexpected errors return a problem containing a traceId."
            )
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> GetInitStatus(
        IQueryHandler<InitStatusQuery, InitStatusResponse> queryHandler,
        CancellationToken cancellationToken
    )
    {
        var result = await queryHandler.Handle(InitStatusQuery.Instance, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblemDetails();
    }

    private static async Task<IResult> RegisterFirstUser(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RegisterFirstUserRequest request,
        ICommandHandler<RegisterFirstUserCommand> commandHandler,
        CancellationToken cancellationToken
    )
    {
        // request will be null if the body is empty, but the parameter is non-nullable to generate correct OpenAPI documentation
        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        var command = new RegisterFirstUserCommand(request?.Username, request?.Password);
        var result = await commandHandler.Handle(command, cancellationToken);
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblemDetails();
    }

    [UsedImplicitly]
    [Description("Credentials for the initial administrator.")]
    private sealed record RegisterFirstUserRequest(
        [property: Description("Required username for the initial administrator.")] string Username,
        [property: Description("Required password that satisfies the configured identity policy.")]
            string Password
    );
}
