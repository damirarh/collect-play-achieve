using CollectPlayAchieve.Api.Infrastructure;
using JetBrains.Annotations;

namespace CollectPlayAchieve.Api.Features.Auth;

internal static class InitEndpoints
{
    extension(IEndpointRouteBuilder builder)
    {
        public IEndpointRouteBuilder MapInitEndpoints()
        {
            builder.MapGet("/init/status", GetInitStatus);
            builder.MapPost("/init/register", RegisterFirstUser);

            return builder;
        }
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
        RegisterFirstUserRequest? request,
        ICommandHandler<RegisterFirstUserCommand> commandHandler,
        CancellationToken cancellationToken
    )
    {
        var command = new RegisterFirstUserCommand(request?.Username, request?.Password);
        var result = await commandHandler.Handle(command, cancellationToken);
        return result.IsSuccess ? TypedResults.Ok() : result.ToProblemDetails();
    }

    [UsedImplicitly]
    private sealed record RegisterFirstUserRequest(string? Username, string? Password);
}
