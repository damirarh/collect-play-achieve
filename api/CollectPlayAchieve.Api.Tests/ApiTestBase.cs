using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using TUnit.AspNetCore;

namespace CollectPlayAchieve.Api.Tests;

internal abstract class ApiTestBase
{
    private TemporaryDataDirectory? _dataDirectory;
    private TestFactory? _factory;

    protected TestFactory Factory =>
        _factory
        ?? throw new InvalidOperationException("The test factory has not been initialized.");

    protected string DatabasePath =>
        _dataDirectory?.DatabasePath
        ?? throw new InvalidOperationException("The test data directory has not been initialized.");

    [Before(Test)]
    public void SetUpApi()
    {
        _dataDirectory = new TemporaryDataDirectory();
        _factory = new TestFactory(_dataDirectory.Path);
    }

    [After(Test)]
    public async Task TearDownApiAsync()
    {
        try
        {
            if (_factory is not null)
            {
                await _factory.DisposeAsync();
            }
        }
        finally
        {
            _dataDirectory?.Dispose();
        }
    }

    protected sealed class TestFactory(string dataFolder) : TestWebApplicationFactory<Program>
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

    private sealed class TemporaryDataDirectory : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("cpa-api-");

        public string Path => _directory.FullName;

        public string DatabasePath => System.IO.Path.Combine(Path, "db", "collect-play-achieve.db");

        public void Dispose()
        {
            try
            {
                using var connection = new SqliteConnection(
                    new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString()
                );
                SqliteConnection.ClearPool(connection);
            }
            finally
            {
                _directory.Delete(recursive: true);
            }
        }
    }
}
