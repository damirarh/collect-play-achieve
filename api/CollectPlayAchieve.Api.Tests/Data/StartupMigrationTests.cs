using CollectPlayAchieve.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CollectPlayAchieve.Api.Tests.Data;

internal class StartupMigrationTests : ApiTestBase
{
    [Test]
    public async Task StartingApi_AppliesAllMigrations()
    {
        Factory.StartServer();

        await Assert.That(File.Exists(DatabasePath)).IsTrue();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var migrations = dbContext.Database.GetMigrations().Order(StringComparer.Ordinal).ToArray();
        var appliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync())
            .Order(StringComparer.Ordinal)
            .ToArray();
        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();

        await Assert.That(migrations.Length).IsGreaterThan(0);
        await Assert.That(appliedMigrations.SequenceEqual(migrations)).IsTrue();
        await Assert.That(pendingMigrations).IsEmpty();
    }
}
