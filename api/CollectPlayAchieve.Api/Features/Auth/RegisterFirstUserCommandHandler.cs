using System.Data;
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

    public async Task<Result> Handle(
        RegisterFirstUserCommand command,
        CancellationToken cancellationToken
    )
    {
        if (command.Username is null || command.Password is null)
        {
            return Result.Failure(RegistrationErrors.InvalidData());
        }

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
        var createResult = await userManager.CreateAsync(user, command.Password);
        if (!createResult.Succeeded)
        {
            return Result.Failure(RegistrationErrors.InvalidData(createResult.Errors));
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

    private static class RegistrationErrors
    {
        public static Error InvalidData(IEnumerable<IdentityError>? errors = null)
        {
            var errorsList = errors?.ToList() ?? new List<IdentityError>();
            return new Error(
                ErrorType.Validation,
                "Registration.InvalidData",
                "Invalid username or password."
                    + (
                        errorsList.Count != 0
                            ? $" Errors: {string.Join(", ", errorsList.Select(e => $"{e.Code}: {e.Description}"))}"
                            : string.Empty
                    )
            );
        }

        public static readonly Error UserAlreadyExists = new(
            ErrorType.Conflict,
            "Registration.UserAlreadyExists",
            "A user already exists. Registration is not available."
        );
    }
}
