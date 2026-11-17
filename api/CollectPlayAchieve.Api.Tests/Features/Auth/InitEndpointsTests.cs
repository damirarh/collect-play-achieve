using System.Net;
using System.Text.Json;
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
        await Assert.That(registrationResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

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
        await Assert.That(registrationResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var problem = await registrationResponse.Content.ReadFromJsonAsync<JsonElement>();
        await Assert
            .That(problem.GetProperty("title").GetString())
            .IsEqualTo("Registration.InvalidData");
        await Assert
            .That(problem.GetProperty("detail").GetString())
            .IsEqualTo("Invalid username or password.");
        var errors = problem.GetProperty("errors");
        await Assert.That(errors.EnumerateObject().Count()).IsEqualTo(1);
        var descriptions = errors
            .GetProperty(field)
            .EnumerateArray()
            .Select(error => error.GetString())
            .ToArray();
        var describer = new IdentityErrorDescriber();
        string?[] expectedDescriptions =
            field == "username"
                ? [describer.InvalidUserName(username).Description]
                :
                [
                    describer.PasswordTooShort(6).Description,
                    describer.PasswordRequiresNonAlphanumeric().Description,
                    describer.PasswordRequiresDigit().Description,
                    describer.PasswordRequiresUpper().Description,
                ];
        await Assert.That(descriptions).IsEquivalentTo(expectedDescriptions);

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

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = problem.GetProperty("errors");
        await Assert
            .That(errors.EnumerateObject().Count())
            .IsEqualTo((username is null ? 1 : 0) + (password is null ? 1 : 0));
        if (username is null)
        {
            await Assert
                .That(errors.GetProperty("username")[0].GetString())
                .IsEqualTo("Username is required.");
        }

        if (password is null)
        {
            await Assert
                .That(errors.GetProperty("password")[0].GetString())
                .IsEqualTo("Password is required.");
        }

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

        await Assert.That(firstResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(secondResponse.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
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
            .That(responses.Count(response => response.StatusCode == HttpStatusCode.OK))
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
