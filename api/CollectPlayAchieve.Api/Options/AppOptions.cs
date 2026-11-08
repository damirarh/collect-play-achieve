using System.Diagnostics.CodeAnalysis;

namespace CollectPlayAchieve.Api.Options;

[SuppressMessage(
    "Design",
    "CA1812",
    Justification = "Configuration binding exposes this options type through the application's dependency injection container."
)]
internal sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>
    /// Gets or sets the path to the folder where application data is stored.
    /// This can be an absolute path or a '~' home-directory path.
    /// If not specified, defaults to a 'data' folder in the current working directory.
    /// </summary>
    /// <remarks>
    /// Use <see cref="GetDataFolderFullPath"/> to get the full path to the data folder instead.
    /// </remarks>
    public string? DataFolder { get; set; }

    /// <summary>
    /// Gets the full path to the folder where application data is stored.
    /// </summary>
    /// <returns>The full path to the data folder.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the home directory cannot be resolved or the path is not fully qualified.</exception>
    public string GetDataFolderFullPath()
    {
        if (string.IsNullOrWhiteSpace(DataFolder))
        {
            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "data"));
        }

        var path = DataFolder;
        if (
            path == "~"
            || path.StartsWith("~/", StringComparison.Ordinal)
            || path.StartsWith("~\\", StringComparison.Ordinal)
        )
        {
            var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(homeDirectory))
            {
                throw new InvalidOperationException(
                    "Cannot resolve the home directory for the DataFolder setting."
                );
            }

            path = path == "~" ? homeDirectory : Path.Combine(homeDirectory, path[2..]);
        }

        if (!Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException(
                "The DataFolder setting must be an absolute path or a '~' home-directory path."
            );
        }

        return Path.GetFullPath(path);
    }
}
