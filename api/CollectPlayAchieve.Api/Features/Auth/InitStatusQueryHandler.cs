using System.ComponentModel;
using CollectPlayAchieve.Api.Data;
using CollectPlayAchieve.Api.Infrastructure;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;

namespace CollectPlayAchieve.Api.Features.Auth;

internal sealed record InitStatusQuery : IQuery<InitStatusResponse>
{
    private InitStatusQuery() { }

    public static InitStatusQuery Instance { get; } = new();
}

[Description("Availability of initial administrator registration.")]
internal sealed record InitStatusResponse(
    [property: UsedImplicitly]
    [property: Description("True when no user exists and initial registration is available.")]
        bool RegistrationAvailable
);

internal class InitStatusQueryHandler(AppDbContext dbContext)
    : IQueryHandler<InitStatusQuery, InitStatusResponse>
{
    public async Task<Result<InitStatusResponse>> Handle(
        InitStatusQuery query,
        CancellationToken cancellationToken
    )
    {
        var registrationAvailable = !await dbContext.Users.AnyAsync(cancellationToken);
        return Result<InitStatusResponse>.Success(new InitStatusResponse(registrationAvailable));
    }
}
