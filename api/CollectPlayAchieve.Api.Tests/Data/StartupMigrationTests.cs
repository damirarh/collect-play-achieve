using CollectPlayAchieve.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TUnit.AspNetCore;

namespace CollectPlayAchieve.Api.Tests.Data;

internal class StartupMigrationTests
{
    [Test]
    public async Task StartingApi_AppliesAllMigrations()
    {
        var dataDirectory = Directory.CreateTempSubdirectory("cpa-migrations-");
        var databasePath = Path.Combine(dataDirectory.FullName, "db", "collect-play-achieve.db");

        try
        {
            await using var factory = new MigrationTestFactory(dataDirectory.FullName);
            factory.StartServer();

            await Assert.That(File.Exists(databasePath)).IsTrue();

            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var migrations = dbContext
                .Database.GetMigrations()
                .Order(StringComparer.Ordinal)
                .ToArray();
            var appliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync())
                .Order(StringComparer.Ordinal)
                .ToArray();
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();

            await Assert.That(migrations.Length).IsGreaterThan(0);
            await Assert.That(appliedMigrations.SequenceEqual(migrations)).IsTrue();
            await Assert.That(pendingMigrations).IsEmpty();
        }
        finally
        {
            // clean pool to ensure the database file can be deleted on Windows
            await using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString()
            );
            SqliteConnection.ClearPool(connection);
            dataDirectory.Delete(recursive: true);
        }
    }

    private sealed class MigrationTestFactory(string dataFolder)
        : TestWebApplicationFactory<Program>
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
}
