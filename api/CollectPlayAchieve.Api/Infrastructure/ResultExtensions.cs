namespace CollectPlayAchieve.Api.Infrastructure;

internal static class ResultExtensions
{
    extension(Result result)
    {
        public IResult ToProblemDetails()
        {
            return TypedResults.Problem(
                title: result.Error.Code,
                detail: result.Error.Description,
                statusCode: GetStatusCode(result.Error.Type)
            );
        }
    }

    private static int GetStatusCode(ErrorType errorType) =>
        errorType switch
        {
            ErrorType.None => StatusCodes.Status200OK,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };
}
