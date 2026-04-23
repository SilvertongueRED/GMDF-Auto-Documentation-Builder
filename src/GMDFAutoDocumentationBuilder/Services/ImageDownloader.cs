namespace GMDFAutoDocumentationBuilder.Services;

/// <summary>
/// Downloads a remote image to a local file path, creating parent directories as needed.
/// </summary>
public sealed class ImageDownloader
{
    private readonly HttpClient _httpClient;

    public ImageDownloader(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// Attempts to download the image at <paramref name="url"/> and save it to
    /// <paramref name="targetPath"/>. Returns <c>true</c> on success.
    /// </summary>
    public async Task<bool> TryDownloadAsync(string url, string targetPath, CancellationToken cancellationToken)
    {
        try
        {
            var directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return false;

            await using var fileStream = File.Create(targetPath);
            await response.Content.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            // Banner images are optional — if the download fails for any reason
            // (network error, invalid URL, permission issue, etc.) we silently
            // skip the headerImage rather than failing the whole documentation
            // generation run for the mod.
            return false;
        }
    }
}
