using CollectPlayAchieve.Api.Options;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CollectPlayAchieve.Api.Data;

internal static class AppDbContextExtensions
{
    extension(IServiceCollection serviceCollection)
    {
        public IServiceCollection AddAppDbContext()
        {
            return serviceCollection.AddDbContext<AppDbContext>(
                (serviceProvider, options) =>
                {
                    var appOptions = serviceProvider
                        .GetRequiredService<IOptions<AppOptions>>()
                        .Value;
                    var databaseDirectory = Path.Combine(appOptions.GetDataFolderFullPath(), "db");
                    Directory.CreateDirectory(databaseDirectory);

                    var connectionString = new SqliteConnectionStringBuilder
                    {
                        DataSource = Path.Combine(databaseDirectory, "collect-play-achieve.db"),
                    }.ToString();

                    options.UseSqlite(connectionString);
                }
            );
        }
    }

    extension(WebApplication app)
    {
        public async Task MigrateDatabaseAsync()
        {
            await using var scope = app.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.MigrateAsync();
        }
    }
}
