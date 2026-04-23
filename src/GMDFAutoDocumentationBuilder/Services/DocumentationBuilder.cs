using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using GMDFAutoDocumentationBuilder.Models;

namespace GMDFAutoDocumentationBuilder.Services;

public sealed class DocumentationBuilder
{
    private static readonly Regex OrderedListLineRegex = new(@"^\d+\.\s+", RegexOptions.Compiled);

    /// <summary>
    /// Builds a GMDF document from mod manifest data and Nexus metadata.
    /// </summary>
    /// <param name="manifest">Parsed mod manifest.</param>
    /// <param name="nexus">Nexus metadata (may be empty for mods without a Nexus ID).</param>
    /// <param name="localBannerPath">
    /// Path to a locally downloaded banner image relative to the mod folder (e.g.
    /// <c>assets/gmdf_banner.jpg</c>). When provided the Overview page gets a
    /// <c>headerImage</c>. Pass <c>null</c> to omit.
    /// </param>
    public GmdfDocument Build(ModManifestInfo manifest, NexusModInfo nexus, string? localBannerPath = null)
    {
        var headerImage = localBannerPath is not null ? new HeaderImageRef(localBannerPath) : null;
        var overviewEntries = BuildOverviewEntries(manifest, nexus);

        var pages = new List<DocumentationPage>
        {
            new DocumentationPage("Overview", overviewEntries, HeaderImage: headerImage)
        };

        var descriptionEntries = BuildDescriptionEntries(nexus);
        if (descriptionEntries.Count > 0)
            pages.Add(new DocumentationPage("Description", descriptionEntries));

        return new GmdfDocument(
            ModName: FirstNonEmpty(nexus.ModName, manifest.Name, "Unknown Mod"),
            Pages: pages);
    }

    private static IReadOnlyList<object> BuildOverviewEntries(ModManifestInfo manifest, NexusModInfo nexus)
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

        return entries;
    }

    // Builds a structured "Description" page from the full Nexus description when it
    // contains substantially more content than the short summary already shown on the
    // Overview page.
    private static IReadOnlyList<object> BuildDescriptionEntries(NexusModInfo nexus)
    {
        if (string.IsNullOrWhiteSpace(nexus.FullDescription))
            return Array.Empty<object>();

        var summary = FirstNonEmpty(nexus.Summary, "");

        // Skip the description page when the full text is not meaningfully longer
        // than the summary already rendered on the Overview page.
        if (nexus.FullDescription.Length <= summary.Length + 100)
            return Array.Empty<object>();

        return ParseDescriptionToEntries(nexus.FullDescription);
    }

    // Converts a cleaned (HTML/BBCode-stripped) description string into a list of
    // GMDF entry objects by detecting common structural patterns.
    private static IReadOnlyList<object> ParseDescriptionToEntries(string text)
    {
        var entries = new List<object>();

        // Descriptions from NexusMetadataProvider already have normalised newlines.
        var blocks = text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            var trimmed = block.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            var lines = trimmed
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            if (lines.Count == 0)
                continue;

            // Single short line with no trailing sentence-end character → section title.
            if (lines.Count == 1 && IsSectionHeading(lines[0]))
            {
                entries.Add(new SectionTitleEntry(lines[0]));
                continue;
            }

            // All lines are bullet items → unordered list.
            if (lines.Count > 1 && lines.All(l => l.StartsWith("- ") || l.StartsWith("* ")))
            {
                var items = lines
                    .Select(l => l[2..].Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();

                if (items.Count > 0)
                {
                    entries.Add(new ListEntry(items));
                    continue;
                }
            }

            // All lines match "N. text" → ordered list.
            if (lines.Count > 1 && lines.All(l => OrderedListLineRegex.IsMatch(l)))
            {
                var items = lines
                    .Select(l => OrderedListLineRegex.Replace(l, "").Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();

                if (items.Count > 0)
                {
                    entries.Add(new OrderedListEntry(items));
                    continue;
                }
            }

            // Default: join the lines of the block into a single paragraph.
            entries.Add(new ParagraphEntry(string.Join(" ", lines)));
        }

        return entries;
    }

    private static bool IsSectionHeading(string line)
    {
        return line.Length is > 2 and < 70
            && !line.EndsWith('.')
            && !line.EndsWith(',')
            && !line.EndsWith('!')
            && !line.StartsWith("- ")
            && !line.StartsWith("* ")
            && !OrderedListLineRegex.IsMatch(line);
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

// ─── Document root ────────────────────────────────────────────────────────────

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

// ─── Page ─────────────────────────────────────────────────────────────────────

public sealed record DocumentationPage(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("entries")] IReadOnlyList<object> Entries,
    [property: JsonPropertyName("id")] string? Id = null,
    [property: JsonPropertyName("headerImage")] HeaderImageRef? HeaderImage = null);

/// <summary>Reference to an image texture shown as the page banner.</summary>
public sealed record HeaderImageRef(
    [property: JsonPropertyName("texture")] string Texture);

// ─── Entry types ──────────────────────────────────────────────────────────────

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

public sealed record CaptionEntry([property: JsonPropertyName("text")] string Text)
{
    [JsonPropertyName("type")]
    public string Type => "caption";
}

public sealed record DividerEntry
{
    [JsonPropertyName("type")]
    public string Type => "divider";
}

public sealed record SpacerEntry(
    [property: JsonPropertyName("height")] int Height = 16)
{
    [JsonPropertyName("type")]
    public string Type => "spacer";
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

public sealed record OrderedListEntry([property: JsonPropertyName("items")] IReadOnlyList<string> Items)
{
    [JsonPropertyName("type")]
    public string Type => "orderedList";
}

public sealed record SpoilerEntry(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("text")] string Text)
{
    [JsonPropertyName("type")]
    public string Type => "spoiler";
}

public sealed record LinkEntry(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("url")] string Url)
{
    [JsonPropertyName("type")]
    public string Type => "link";
}

public sealed record ImageEntry(
    [property: JsonPropertyName("texture")] string Texture,
    [property: JsonPropertyName("scale")] double? Scale = null,
    [property: JsonPropertyName("align")] string? Align = null)
{
    [JsonPropertyName("type")]
    public string Type => "image";
}

public sealed record GifEntry(
    [property: JsonPropertyName("texture")] string Texture,
    [property: JsonPropertyName("frameCount")] int FrameCount,
    [property: JsonPropertyName("frameDuration")] double? FrameDuration = null,
    [property: JsonPropertyName("scale")] double? Scale = null)
{
    [JsonPropertyName("type")]
    public string Type => "gif";
}

public sealed record RowEntry(
    [property: JsonPropertyName("left")] IReadOnlyList<object>? Left,
    [property: JsonPropertyName("right")] IReadOnlyList<object>? Right,
    [property: JsonPropertyName("leftFraction")] double? LeftFraction = null)
{
    [JsonPropertyName("type")]
    public string Type => "row";
}

public sealed record IndentBlockEntry(
    [property: JsonPropertyName("entries")] IReadOnlyList<object> Entries,
    [property: JsonPropertyName("indent")] int? Indent = null)
{
    [JsonPropertyName("type")]
    public string Type => "indentBlock";
}

