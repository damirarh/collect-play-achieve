using System.Data;
using CollectPlayAchieve.Api.Data;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CollectPlayAchieve.Api.Endpoints;

internal static class InitEndpoints
{
    private const string AdministratorRole = "Administrator";

    extension(IEndpointRouteBuilder builder)
    {
        public IEndpointRouteBuilder MapInitEndpoints()
        {
            builder.MapGet(
                "/init/status",
                async (AppDbContext dbContext, CancellationToken cancellationToken) =>
                {
                    var registrationAvailable = !await dbContext.Users.AnyAsync(cancellationToken);
                    return TypedResults.Ok(new InitStatusResponse(registrationAvailable));
                }
            );

            builder.MapPost("/init/register", Register);

            return builder;
        }
    }

    private static async Task<Results<Ok, Conflict, BadRequest<RegistrationErrors>>> Register(
        RegisterRequest? request,
        AppDbContext dbContext,
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        CancellationToken cancellationToken
    )
    {
        if (request?.Username is null || request.Password is null)
        {
            return TypedResults.BadRequest(
                new RegistrationErrors(["Username and password are required."])
            );
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken
        );

        if (await dbContext.Users.AnyAsync(cancellationToken))
        {
            return TypedResults.Conflict();
        }

        if (!await roleManager.RoleExistsAsync(AdministratorRole))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole(AdministratorRole));
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create the {AdministratorRole} role: {FormatErrors(roleResult)}"
                );
            }
        }

        var user = new IdentityUser { UserName = request.Username };
        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return TypedResults.BadRequest(
                new RegistrationErrors(
                    createResult.Errors.Select(error => error.Description).ToArray()
                )
            );
        }

        var addToRoleResult = await userManager.AddToRoleAsync(user, AdministratorRole);
        if (!addToRoleResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to assign the initial user to the {AdministratorRole} role: {FormatErrors(addToRoleResult)}"
            );
        }

        await transaction.CommitAsync(cancellationToken);
        return TypedResults.Ok();
    }

    private static string FormatErrors(IdentityResult result) =>
        string.Join(", ", result.Errors.Select(error => error.Description));

    private sealed record InitStatusResponse([property: UsedImplicitly] bool RegistrationAvailable);

    [UsedImplicitly]
    private sealed record RegisterRequest(string? Username, string? Password);

    private sealed record RegistrationErrors([property: UsedImplicitly] string[] Errors);
}
