using System.Net;
using CollectPlayAchieve.Api.Data;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CollectPlayAchieve.Api.Tests.Features.Auth;

internal sealed class InitEndpointsTests : ApiTestBase
{
    [Test]
    public async Task Register_CreatesFirstUserAsAdministrator_AndDisablesRegistration()
    {
        using var client = Factory.CreateClient();

        var initialStatusResponse = await client.GetAsync("/init/status");
        await Assert.That(initialStatusResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var initialStatus =
            await initialStatusResponse.Content.ReadFromJsonAsync<InitStatusResponse>();
        await Assert.That(initialStatus?.RegistrationAvailable).IsTrue();

        var registrationResponse = await client.PostAsJsonAsync(
            "/init/register",
            new RegisterRequest("admin", "SecurePass123!")
        );
        await Assert.That(registrationResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        var finalStatusResponse = await client.GetAsync("/init/status");
        await Assert.That(finalStatusResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var finalStatus = await finalStatusResponse.Content.ReadFromJsonAsync<InitStatusResponse>();
        await Assert.That(finalStatus?.RegistrationAvailable).IsFalse();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await userManager.FindByNameAsync("admin");
        await Assert.That(await dbContext.Users.CountAsync()).IsEqualTo(1);
        await Assert.That(user).IsNotNull();
        await Assert.That(await userManager.IsInRoleAsync(user!, "Administrator")).IsTrue();
    }

    [Test]
    [Arguments("admin", "weak", "password")]
    [Arguments("invalid name", "SecurePass123!", "username")]
    public async Task Register_WithInvalidCredentials_ReturnsFieldErrors_AndKeepsRegistrationAvailable(
        string username,
        string password,
        string field
    )
    {
        using var client = Factory.CreateClient();

        var registrationResponse = await client.PostAsJsonAsync(
            "/init/register",
            new RegisterRequest(username, password)
        );

        var describer = new IdentityErrorDescriber();
        string[] expectedDescriptions =
            field == "username"
                ? [describer.InvalidUserName(username).Description]
                :
                [
                    describer.PasswordTooShort(6).Description,
                    describer.PasswordRequiresNonAlphanumeric().Description,
                    describer.PasswordRequiresDigit().Description,
                    describer.PasswordRequiresUpper().Description,
                ];
        IReadOnlyDictionary<string, string[]> expectedErrors = new Dictionary<string, string[]>
        {
            [field] = expectedDescriptions,
        };
        await Assert
            .That(registrationResponse)
            .MatchesValidationProblemDetails(
                (int)HttpStatusCode.BadRequest,
                expectedErrors,
                title: "Common.ValidationFailed",
                detail: "One or more validation errors occurred."
            );

        var statusResponse = await client.GetAsync("/init/status");
        var status = await statusResponse.Content.ReadFromJsonAsync<InitStatusResponse>();
        await Assert.That(status?.RegistrationAvailable).IsTrue();
    }

    [Test]
    [Arguments(null, "SecurePass123!")]
    [Arguments("admin", null)]
    [Arguments(null, null)]
    public async Task Register_WithNullCredentials_ReturnsRequiredFieldErrors(
        string? username,
        string? password
    )
    {
        using var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/init/register",
            new RegisterRequest(username, password)
        );

        var expectedErrors = new Dictionary<string, string[]>();
        if (username is null)
        {
            expectedErrors["username"] = ["Username is required."];
        }

        if (password is null)
        {
            expectedErrors["password"] = ["Password is required."];
        }

        await Assert
            .That(response)
            .MatchesValidationProblemDetails(
                (int)HttpStatusCode.BadRequest,
                expectedErrors,
                title: "Common.ValidationFailed",
                detail: "One or more validation errors occurred."
            );

        var statusResponse = await client.GetAsync("/init/status");
        var status = await statusResponse.Content.ReadFromJsonAsync<InitStatusResponse>();
        await Assert.That(status?.RegistrationAvailable).IsTrue();
    }

    [Test]
    public async Task Register_WhenAUserAlreadyExists_ReturnsConflict()
    {
        using var client = Factory.CreateClient();
        var request = new RegisterRequest("admin", "SecurePass123!");

        var firstResponse = await client.PostAsJsonAsync("/init/register", request);
        var secondResponse = await client.PostAsJsonAsync("/init/register", request);

        await Assert.That(firstResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert
            .That(secondResponse)
            .MatchesProblemDetails(
                (int)HttpStatusCode.Conflict,
                title: "Registration.UserAlreadyExists",
                detail: "A user already exists. Registration is not available."
            );
    }

    [Test]
    public async Task Register_ConcurrentRequests_CreateOnlyOneUser()
    {
        using var client = Factory.CreateClient();
        var request = new RegisterRequest("admin", "SecurePass123!");

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/init/register", request),
            client.PostAsJsonAsync("/init/register", request)
        );

        await Assert
            .That(responses.Count(response => response.StatusCode == HttpStatusCode.NoContent))
            .IsEqualTo(1);
        await Assert
            .That(responses.Count(response => response.StatusCode == HttpStatusCode.Conflict))
            .IsEqualTo(1);
    }

    private sealed record InitStatusResponse(bool RegistrationAvailable);

    private sealed record RegisterRequest(
        [property: UsedImplicitly] string? Username,
        [property: UsedImplicitly] string? Password
    );
}
