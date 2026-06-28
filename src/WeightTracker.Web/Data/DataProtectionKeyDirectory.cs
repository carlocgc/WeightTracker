using Microsoft.Data.Sqlite;

namespace WeightTracker.Web.Data;

public static class DataProtectionKeyDirectory
{
    public static DirectoryInfo Resolve(string? configuredKeysPath, string connectionString, string contentRootPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredKeysPath))
        {
            var keysPath = Path.IsPathFullyQualified(configuredKeysPath)
                ? configuredKeysPath
                : Path.GetFullPath(Path.Combine(contentRootPath, configuredKeysPath));
            return new DirectoryInfo(keysPath);
        }

        return Resolve(connectionString, contentRootPath);
    }

    public static DirectoryInfo Resolve(string connectionString, string contentRootPath)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource == ":memory:")
        {
            return ResolveFallback(contentRootPath);
        }

        var databasePath = Path.IsPathFullyQualified(builder.DataSource)
            ? builder.DataSource
            : Path.GetFullPath(Path.Combine(contentRootPath, builder.DataSource));
        var databaseDirectory = Path.GetDirectoryName(databasePath);

        return string.IsNullOrWhiteSpace(databaseDirectory)
            ? ResolveFallback(contentRootPath)
            : new DirectoryInfo(Path.Combine(databaseDirectory, "DataProtectionKeys"));
    }

    private static DirectoryInfo ResolveFallback(string contentRootPath)
    {
        return new DirectoryInfo(Path.GetFullPath(Path.Combine(contentRootPath, "App_Data", "DataProtectionKeys")));
    }
}
