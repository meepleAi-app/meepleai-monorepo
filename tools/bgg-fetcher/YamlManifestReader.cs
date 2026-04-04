using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace BggFetcher;

public static class YamlManifestReader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
        .Build();

    public static string GetManifestPath(string manifestName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
            dir = dir.Parent;
        var repoRoot = dir?.FullName ?? throw new InvalidOperationException("Could not find repo root (.git directory)");
        return Path.Combine(repoRoot, "apps", "api", "src", "Api", "Infrastructure", "Seeders", "Catalog", "Manifests", $"{manifestName}.yml");
    }

    public static FetcherManifest Load(string filePath)
    {
        var yaml = File.ReadAllText(filePath);
        return Deserializer.Deserialize<FetcherManifest>(yaml);
    }

    /// <summary>
    /// WARNING: YamlDotNet strips comments and may reorder keys.
    /// </summary>
    public static void Save(string filePath, FetcherManifest manifest)
    {
        var yaml = Serializer.Serialize(manifest);
        File.WriteAllText(filePath, yaml);
    }
}
