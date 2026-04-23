using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using GMDFAutoDocumentationBuilder.Models;
using StardewModdingAPI;

namespace GMDFAutoDocumentationBuilder.Services;

public sealed class NexusMetadataProvider
{
    private readonly HttpClient _httpClient;
    private static readonly Regex HtmlMetaPropertyRegex = new("<meta\\s+property=\"og:title\"\\s+content=\"([^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HtmlOgImageRegex = new("<meta\\s+property=\"og:image\"\\s+content=\"([^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HtmlDescriptionRegex = new("<meta\\s+name=\"description\"\\s+content=\"([^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HtmlMetaDescriptionRegex = new("<meta\\s+property=\"og:description\"\\s+content=\"([^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HtmlListItemRegex = new("<li[^>]*>(.*?)</li>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new("\\s+", RegexOptions.Compiled);
    private static readonly Regex BbCodeListItemRegex = new(@"\[\*\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BbCodeHrRegex = new(@"\[hr\]|\[rule\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BbCodeTagRegex = new(@"\[/?[a-zA-Z][^\]]*\]", RegexOptions.Compiled);
    private static readonly Regex MultipleNewlineRegex = new(@"\n{3,}", RegexOptions.Compiled);
    private static readonly Regex TrailingSpaceOnLineRegex = new(@"[^\S\n]+\n", RegexOptions.Compiled);

    public NexusMetadataProvider(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<NexusModInfo> FetchAsync(ModManifestInfo manifest, string? nexusApiKey, IMonitor monitor, CancellationToken cancellationToken)
    {
        if (manifest.NexusModId is null)
            return new NexusModInfo(null, null, Array.Empty<string>(), null);

        var nexusId = manifest.NexusModId.Value;

        if (!string.IsNullOrWhiteSpace(nexusApiKey))
        {
            var apiResult = await TryFetchFromApiAsync(nexusId, nexusApiKey, monitor, cancellationToken).ConfigureAwait(false);
            if (apiResult is not null)
                return apiResult;
        }

        return await FetchFromPageAsync(nexusId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<NexusModInfo?> TryFetchFromApiAsync(int nexusId, string nexusApiKey, IMonitor monitor, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.nexusmods.com/v1/games/stardewvalley/mods/{nexusId}.json");
        request.Headers.TryAddWithoutValidation("apikey", nexusApiKey.Trim());

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                monitor.Log($"Nexus API request for mod {nexusId} failed with status {(int)response.StatusCode}. Falling back to HTML scrape.", LogLevel.Warn);
                return null;
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            var name = GetString(root, "name");
            var summary = GetString(root, "summary");
            var description = GetString(root, "description");
            var sourceUrl = GetString(root, "url");
            var pictureUrl = GetString(root, "picture_url");

            var features = ExtractFeatures(description);
            if (features.Count == 0)
                features = ExtractFeatures(summary);

            var fullDescription = DecodeDescription(description);

            return new NexusModInfo(
                name,
                FirstNonEmpty(summary, description),
                features,
                sourceUrl,
                FullDescription: string.IsNullOrWhiteSpace(fullDescription) ? null : fullDescription,
                PictureUrl: string.IsNullOrWhiteSpace(pictureUrl) ? null : pictureUrl);
        }
        catch (Exception ex)
        {
            monitor.Log($"Nexus API request for mod {nexusId} failed: {ex.Message}. Falling back to HTML scrape.", LogLevel.Warn);
            return null;
        }
    }

    private async Task<NexusModInfo> FetchFromPageAsync(int nexusId, CancellationToken cancellationToken)
    {
        var pageUrl = $"https://www.nexusmods.com/stardewvalley/mods/{nexusId}?tab=description";
        var html = await _httpClient.GetStringAsync(pageUrl, cancellationToken).ConfigureAwait(false);

        var title = Decode(HtmlMetaPropertyRegex.Match(html).Groups[1].Value);
        var summary = Decode(FirstNonEmpty(
            HtmlDescriptionRegex.Match(html).Groups[1].Value,
            HtmlMetaDescriptionRegex.Match(html).Groups[1].Value) ?? string.Empty);

        var pictureUrlRaw = WebUtility.HtmlDecode(HtmlOgImageRegex.Match(html).Groups[1].Value);
        var pictureUrl = string.IsNullOrWhiteSpace(pictureUrlRaw) ? null : pictureUrlRaw.Trim();

        var features = HtmlListItemRegex
            .Matches(html)
            .Select(match => Decode(match.Groups[1].Value))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(15)
            .ToArray();

        return new NexusModInfo(title, summary, features, pageUrl, PictureUrl: pictureUrl);
    }

    private static List<string> ExtractFeatures(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return new List<string>();

        var text = Decode(source);
        var features = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            .Select(line => line[2..].Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(15)
            .ToList();

        return features;
    }

    // Strips HTML and BBCode while preserving paragraph structure and converting
    // list markers to plain-text bullets so the description can be re-parsed
    // by DocumentationBuilder into structured entries.
    private static string DecodeDescription(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        // Convert BBCode list items to bullet-prefixed lines before tag removal.
        var text = BbCodeListItemRegex.Replace(value, "\n- ");

        // Convert horizontal rule tags to paragraph breaks.
        text = BbCodeHrRegex.Replace(text, "\n\n");

        // Strip all remaining BBCode tags.
        text = BbCodeTagRegex.Replace(text, "");

        // Strip HTML tags (some descriptions use inline HTML).
        text = HtmlTagRegex.Replace(text, "");

        // Decode HTML entities.
        text = WebUtility.HtmlDecode(text);

        // Normalize line endings.
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        // Trim trailing whitespace on each line.
        text = TrailingSpaceOnLineRegex.Replace(text, "\n");

        // Collapse three or more consecutive blank lines to a single blank line.
        text = MultipleNewlineRegex.Replace(text, "\n\n");

        return text.Trim();
    }

    private static string Decode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var stripped = HtmlTagRegex.Replace(value, " ");
        stripped = WebUtility.HtmlDecode(stripped);
        stripped = WhitespaceRegex.Replace(stripped, " ");

        return stripped.Trim();
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }

    private static string GetString(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            return value.GetString() ?? string.Empty;

        return string.Empty;
    }
}
