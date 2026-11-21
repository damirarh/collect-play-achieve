using System.Data;
using System.Text.Json;
using CollectPlayAchieve.Api.Data;
using CollectPlayAchieve.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CollectPlayAchieve.Api.Features.Auth;

internal sealed record RegisterFirstUserCommand(string? Username, string? Password) : ICommand { }

internal class RegisterFirstUserCommandHandler(
    AppDbContext dbContext,
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager
) : ICommandHandler<RegisterFirstUserCommand>
{
    private const string AdministratorRole = "Administrator";
    private static readonly string UsernameKey = JsonNamingPolicy.CamelCase.ConvertName(
        nameof(RegisterFirstUserCommand.Username)
    );
    private static readonly string PasswordKey = JsonNamingPolicy.CamelCase.ConvertName(
        nameof(RegisterFirstUserCommand.Password)
    );

    public async Task<Result> Handle(
        RegisterFirstUserCommand command,
        CancellationToken cancellationToken
    )
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );

        if (await dbContext.Users.AnyAsync(cancellationToken))
        {
            return Result.Failure(RegistrationErrors.UserAlreadyExists);
        }

        if (!await roleManager.RoleExistsAsync(AdministratorRole))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole(AdministratorRole));
            if (!roleResult.Succeeded)
            {
                throw new IdentityException(
                    $"Failed to create the {AdministratorRole} role.",
                    roleResult.Errors
                );
            }
        }

        var user = new IdentityUser { UserName = command.Username };
        var createResult = await userManager.CreateAsync(user, command.Password ?? string.Empty);
        if (!createResult.Succeeded)
        {
            return MapCreateResult(createResult);
        }

        var addToRoleResult = await userManager.AddToRoleAsync(user, AdministratorRole);
        if (!addToRoleResult.Succeeded)
        {
            throw new IdentityException(
                $"Failed to assign the initial user to the {AdministratorRole} role.",
                addToRoleResult.Errors
            );
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private static Result MapCreateResult(IdentityResult createResult)
    {
        var errors = createResult.Errors.ToList();
        var validationErrors = new Dictionary<string, string[]>();
        var usernameErrors = errors
            .Where(error => error.Code.Contains("UserName", StringComparison.OrdinalIgnoreCase))
            .Select(error => error.Description)
            .ToArray();
        var passwordErrors = errors
            .Where(error => error.Code.Contains("Password", StringComparison.OrdinalIgnoreCase))
            .Select(error => error.Description)
            .ToArray();

        if (usernameErrors.Length != 0)
        {
            validationErrors[UsernameKey] = usernameErrors;
        }

        if (passwordErrors.Length != 0)
        {
            validationErrors[PasswordKey] = passwordErrors;
        }

        return validationErrors.Count == 0
            ? throw new IdentityException("Failed to create the initial user.", errors)
            : Result.Failure(RegistrationErrors.InvalidData(validationErrors));
    }

    private static class RegistrationErrors
    {
        public static Error InvalidData(IReadOnlyDictionary<string, string[]> validationErrors) =>
            new(
                ErrorType.Validation,
                "Registration.InvalidData",
                "Invalid username or password.",
                validationErrors
            );

        public static readonly Error UserAlreadyExists = new(
            ErrorType.Conflict,
            "Registration.UserAlreadyExists",
            "A user already exists. Registration is not available."
        );
    }
}
