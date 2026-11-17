using CollectPlayAchieve.Api.Data;
using CollectPlayAchieve.Api.Features.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CollectPlayAchieve.Api.Tests.Features.Auth;

internal sealed class RegisterFirstUserCommandHandlerTests : ApiTestBase
{
    [Test]
    public async Task CreateFailure_GroupsMatchingDescriptions_AndIgnoresUnmatchedErrors()
    {
        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var identityResult = IdentityResult.Failed(
            new IdentityError { Code = "InvalidUSERNAME", Description = "Invalid username." },
            new IdentityError { Code = "passwordTooShort", Description = "Password too short." },
            new IdentityError { Code = "StoreFailure", Description = "Internal store details." },
            new IdentityError { Code = "UserNamePassword", Description = "Invalid credentials." }
        );
        using var userManager = ActivatorUtilities.CreateInstance<FailingUserManager>(
            services,
            identityResult
        );
        var handler = new RegisterFirstUserCommandHandler(
            services.GetRequiredService<AppDbContext>(),
            userManager,
            services.GetRequiredService<RoleManager<IdentityRole>>()
        );

        var result = await handler.Handle(
            new RegisterFirstUserCommand("admin", "SecurePass123!"),
            CancellationToken.None
        );

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Code).IsEqualTo("Registration.InvalidData");
        await Assert.That(result.Error.Description).IsEqualTo("Invalid username or password.");
        var errors = result.Error.ValidationErrors!;
        await Assert.That(errors.Count).IsEqualTo(2);
        await Assert.That(errors["username"].Length).IsEqualTo(2);
        await Assert.That(errors["username"][0]).IsEqualTo("Invalid username.");
        await Assert.That(errors["username"][1]).IsEqualTo("Invalid credentials.");
        await Assert.That(errors["password"].Length).IsEqualTo(2);
        await Assert.That(errors["password"][0]).IsEqualTo("Password too short.");
        await Assert.That(errors["password"][1]).IsEqualTo("Invalid credentials.");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CreateFailure_WithoutMatchingCodes_ThrowsIdentityException_AndRollsBack(
        bool emptyErrors
    )
    {
        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var identityErrors = emptyErrors
            ? Array.Empty<IdentityError>()
            : new[]
            {
                new IdentityError
                {
                    Code = "StoreFailure",
                    Description = "Internal store details.",
                },
            };
        using var userManager = ActivatorUtilities.CreateInstance<FailingUserManager>(
            services,
            IdentityResult.Failed(identityErrors)
        );
        var handler = new RegisterFirstUserCommandHandler(
            services.GetRequiredService<AppDbContext>(),
            userManager,
            services.GetRequiredService<RoleManager<IdentityRole>>()
        );
        var expectedMessage =
            "Failed to create the initial user."
            + (
                emptyErrors
                    ? string.Empty
                    : $"{Environment.NewLine}Errors:{Environment.NewLine}StoreFailure: Internal store details."
            );

        await Assert
            .That(async () =>
            {
                await handler.Handle(
                    new RegisterFirstUserCommand("admin", "SecurePass123!"),
                    CancellationToken.None
                );
            })
            .Throws<IdentityException>()
            .WithMessage(expectedMessage);

        using var verificationScope = Factory.Services.CreateScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await Assert.That(await dbContext.Users.CountAsync()).IsEqualTo(0);
        await Assert.That(await dbContext.Roles.CountAsync()).IsEqualTo(0);
    }

    private sealed class FailingUserManager(
        IUserStore<IdentityUser> store,
        IOptions<IdentityOptions> options,
        IPasswordHasher<IdentityUser> passwordHasher,
        ILookupNormalizer normalizer,
        IdentityErrorDescriber errorDescriber,
        IServiceProvider services,
        ILogger<UserManager<IdentityUser>> logger,
        IdentityResult identityResult
    )
        : UserManager<IdentityUser>(
            store,
            options,
            passwordHasher,
            [],
            [],
            normalizer,
            errorDescriber,
            services,
            logger
        )
    {
        public override Task<IdentityResult> CreateAsync(IdentityUser user, string password) =>
            Task.FromResult(identityResult);
    }
}
