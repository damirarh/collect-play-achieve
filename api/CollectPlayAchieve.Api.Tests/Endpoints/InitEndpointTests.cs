using System.Net;
using System.Net.Http.Json;
using CollectPlayAchieve.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TUnit.AspNetCore;

namespace CollectPlayAchieve.Api.Tests.Endpoints;

internal sealed class InitEndpointTests
{
    [Test]
    public async Task Register_CreatesFirstUserAsAdministrator_AndDisablesRegistration()
    {
        using var dataDirectory = new TemporaryDataDirectory();
        await using var factory = new InitTestFactory(dataDirectory.Path);
        using var client = factory.CreateClient();

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

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await userManager.FindByNameAsync("admin");
        await Assert.That(await dbContext.Users.CountAsync()).IsEqualTo(1);
        await Assert.That(user).IsNotNull();
        await Assert.That(await userManager.IsInRoleAsync(user!, "Administrator")).IsTrue();
    }

    [Test]
    public async Task Register_WithInvalidPassword_ReturnsBadRequest_AndKeepsRegistrationAvailable()
    {
        using var dataDirectory = new TemporaryDataDirectory();
        await using var factory = new InitTestFactory(dataDirectory.Path);
        using var client = factory.CreateClient();

        var registrationResponse = await client.PostAsJsonAsync(
            "/init/register",
            new RegisterRequest("admin", "weak")
        );
        await Assert.That(registrationResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);

        var statusResponse = await client.GetAsync("/init/status");
        var status = await statusResponse.Content.ReadFromJsonAsync<InitStatusResponse>();
        await Assert.That(status?.RegistrationAvailable).IsTrue();
    }

    [Test]
    public async Task Register_WhenAUserAlreadyExists_ReturnsConflict()
    {
        using var dataDirectory = new TemporaryDataDirectory();
        await using var factory = new InitTestFactory(dataDirectory.Path);
        using var client = factory.CreateClient();
        var request = new RegisterRequest("admin", "SecurePass123!");

        var firstResponse = await client.PostAsJsonAsync("/init/register", request);
        var secondResponse = await client.PostAsJsonAsync("/init/register", request);

        await Assert.That(firstResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(secondResponse.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Register_ConcurrentRequests_CreateOnlyOneUser()
    {
        using var dataDirectory = new TemporaryDataDirectory();
        await using var factory = new InitTestFactory(dataDirectory.Path);
        using var client = factory.CreateClient();
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

    private sealed class InitTestFactory(string dataFolder) : TestWebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?> { ["App:DataFolder"] = dataFolder }
                    )
            );
        }
    }

    private sealed record InitStatusResponse(bool RegistrationAvailable);

    private sealed record RegisterRequest(string Username, string Password);

    private sealed class TemporaryDataDirectory : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("cpa-init-");

        public string Path => directory.FullName;

        public void Dispose() => directory.Delete(recursive: true);
    }
}
