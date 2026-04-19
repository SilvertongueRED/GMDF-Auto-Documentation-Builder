using System.Text.Json;
using GMDFAutoDocumentationBuilder.Models;

namespace GMDFAutoDocumentationBuilder.Services;

public sealed class ManifestScanner
{
    public IReadOnlyList<ModManifestInfo> Scan(string modsDirectory)
    {
        if (string.IsNullOrWhiteSpace(modsDirectory) || !Directory.Exists(modsDirectory))
            return Array.Empty<ModManifestInfo>();

        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(modsDirectory)
        };

        var stardropDirectory = Path.Combine(modsDirectory, "Stardrop Installed Mods");
        if (Directory.Exists(stardropDirectory))
            roots.Add(Path.GetFullPath(stardropDirectory));

        var seenManifestDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var manifests = new List<ModManifestInfo>();

        foreach (var root in roots)
        {
            foreach (var manifestPath in Directory.EnumerateFiles(root, "manifest.json", SearchOption.AllDirectories))
            {
                var manifestDirectory = Path.GetDirectoryName(manifestPath);
                if (manifestDirectory is null || !seenManifestDirectories.Add(manifestDirectory))
                    continue;

                if (TryParseManifest(manifestPath, out var manifest))
                    manifests.Add(manifest);
            }
        }

        return manifests;
    }

    public static int? TryGetNexusModId(IReadOnlyList<string> updateKeys)
    {
        foreach (var updateKey in updateKeys)
        {
            if (!updateKey.StartsWith("Nexus:", StringComparison.OrdinalIgnoreCase))
                continue;

            var rawId = updateKey["Nexus:".Length..].Trim();
            if (int.TryParse(rawId, out var id) && id > 0)
                return id;
        }

        return null;
    }

    private static bool TryParseManifest(string manifestPath, out ModManifestInfo manifest)
    {
        manifest = null!;

        try
        {
            using var stream = File.OpenRead(manifestPath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            var updateKeys = ParseUpdateKeys(root);
            var dependencies = ParseDependencies(root);

            manifest = new ModManifestInfo(
                DirectoryPath: Path.GetDirectoryName(manifestPath)!,
                Name: GetString(root, "Name"),
                Author: GetString(root, "Author"),
                Description: GetString(root, "Description"),
                Version: GetString(root, "Version"),
                UniqueId: GetString(root, "UniqueID"),
                UpdateKeys: updateKeys,
                Dependencies: dependencies,
                NexusModId: TryGetNexusModId(updateKeys)
            );

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IReadOnlyList<string> ParseUpdateKeys(JsonElement root)
    {
        if (!root.TryGetProperty("UpdateKeys", out var updateKeysElement) || updateKeysElement.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        return updateKeysElement
            .EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString() ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
    }

    private static IReadOnlyList<string> ParseDependencies(JsonElement root)
    {
        if (!root.TryGetProperty("Dependencies", out var dependenciesElement) || dependenciesElement.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var dependencies = new List<string>();

        foreach (var entry in dependenciesElement.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.String)
            {
                var dependency = entry.GetString();
                if (!string.IsNullOrWhiteSpace(dependency))
                    dependencies.Add(dependency);
                continue;
            }

            if (entry.ValueKind != JsonValueKind.Object)
                continue;

            if (entry.TryGetProperty("UniqueID", out var uniqueIdElement) && uniqueIdElement.ValueKind == JsonValueKind.String)
            {
                var uniqueId = uniqueIdElement.GetString();
                if (!string.IsNullOrWhiteSpace(uniqueId))
                    dependencies.Add(uniqueId);
            }
        }

        return dependencies;
    }

    private static string GetString(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            return value.GetString() ?? string.Empty;

        return string.Empty;
    }
}
