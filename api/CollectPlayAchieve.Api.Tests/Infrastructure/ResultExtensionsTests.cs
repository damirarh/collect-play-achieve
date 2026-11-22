using CollectPlayAchieve.Api.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CollectPlayAchieve.Api.Tests.Infrastructure;

internal sealed class ResultExtensionsTests
{
    [Test]
    public async Task Validation_WithoutFieldErrors_ReturnsValidationProblemWithEmptyErrors()
    {
        var result = Result.Failure(TestError.ValidationWithoutFieldErrors);

        var response = (ValidationProblem)result.ToProblemDetails();

        await Assert.That(response.StatusCode).IsEqualTo(400);
        await Assert.That(response.ProblemDetails.Title).IsEqualTo("Invalid");
        await Assert.That(response.ProblemDetails.Detail).IsEqualTo("Invalid input.");
        await Assert.That(response.ProblemDetails.Errors.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Validation_WithFieldErrors_PreservesFieldsAndMessagesInValidationProblem()
    {
        var validationErrors = new Dictionary<string, string[]>
        {
            ["username"] = ["Username is required."],
            ["password"] = ["Password is too short.", "Password requires a digit."],
        };
        var result = Result.Failure(Error.Common.ValidationFailed(validationErrors));

        var response = (ValidationProblem)result.ToProblemDetails();

        await Assert.That(response.ProblemDetails.Errors.Count).IsEqualTo(2);
        await Assert
            .That(response.ProblemDetails.Errors["username"])
            .IsEquivalentTo(validationErrors["username"]);
        await Assert
            .That(response.ProblemDetails.Errors["password"])
            .IsEquivalentTo(validationErrors["password"]);
    }

    [Test]
    public async Task Conflict_PreservesOrdinaryProblemDetails()
    {
        var result = Result.Failure(TestError.Conflict);

        var response = (ProblemHttpResult)result.ToProblemDetails();

        await Assert.That(response.StatusCode).IsEqualTo(409);
        await Assert.That(response.ProblemDetails.Title).IsEqualTo("Conflict");
        await Assert.That(response.ProblemDetails.Detail).IsEqualTo("Already exists.");
    }
}
