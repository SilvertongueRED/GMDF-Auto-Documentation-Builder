using System.Text.Json.Serialization;
using GMDFAutoDocumentationBuilder.Models;

namespace GMDFAutoDocumentationBuilder.Services;

public sealed class DocumentationBuilder
{
    public GmdfDocument Build(ModManifestInfo manifest, NexusModInfo nexus)
    {
        var entries = new List<object>
        {
            new SectionTitleEntry("Overview"),
            new ParagraphEntry(FirstNonEmpty(nexus.Summary, manifest.Description, "No description available.")),
            new DividerEntry(),
            new KeyValueEntry("Author", ValueOrUnknown(manifest.Author)),
            new KeyValueEntry("Version", ValueOrUnknown(manifest.Version)),
            new KeyValueEntry("Unique ID", ValueOrUnknown(manifest.UniqueId))
        };

        if (manifest.NexusModId is not null)
            entries.Add(new KeyValueEntry("Nexus ID", manifest.NexusModId.Value.ToString()));

        if (manifest.Dependencies.Count > 0)
            entries.Add(new KeyValueEntry("Dependencies", string.Join(", ", manifest.Dependencies)));

        if (nexus.Features.Count > 0)
        {
            entries.Add(new SectionTitleEntry("Features"));
            entries.Add(new ListEntry(nexus.Features));
        }

        if (!string.IsNullOrWhiteSpace(nexus.SourceUrl))
            entries.Add(new LinkEntry("Nexus Mods Page", nexus.SourceUrl));

        return new GmdfDocument(
            ModName: FirstNonEmpty(nexus.ModName, manifest.Name, "Unknown Mod"),
            Pages: new[]
            {
                new DocumentationPage("Overview", entries)
            });
    }

    private static string ValueOrUnknown(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}

public sealed record GmdfDocument(
    [property: JsonPropertyName("$schema")] string Schema,
    [property: JsonPropertyName("format")] int Format,
    [property: JsonPropertyName("modName")] string ModName,
    [property: JsonPropertyName("pages")] IReadOnlyList<DocumentationPage> Pages)
{
    public GmdfDocument(string ModName, IReadOnlyList<DocumentationPage> Pages)
        : this(
            "https://raw.githubusercontent.com/vapor64/GMDF/master/documentation.schema.json",
            1,
            ModName,
            Pages)
    {
    }
}

public sealed record DocumentationPage(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("entries")] IReadOnlyList<object> Entries);

public sealed record SectionTitleEntry([property: JsonPropertyName("text")] string Text)
{
    [JsonPropertyName("type")]
    public string Type => "sectionTitle";
}

public sealed record ParagraphEntry([property: JsonPropertyName("text")] string Text)
{
    [JsonPropertyName("type")]
    public string Type => "paragraph";
}

public sealed record DividerEntry
{
    [JsonPropertyName("type")]
    public string Type => "divider";
}

public sealed record KeyValueEntry(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("value")] string Value)
{
    [JsonPropertyName("type")]
    public string Type => "keyValue";
}

public sealed record ListEntry([property: JsonPropertyName("items")] IReadOnlyList<string> Items)
{
    [JsonPropertyName("type")]
    public string Type => "list";
}

public sealed record LinkEntry(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("url")] string Url)
{
    [JsonPropertyName("type")]
    public string Type => "link";
}
