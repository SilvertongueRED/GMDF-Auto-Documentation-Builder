using System.Text;
using GMDFAutoDocumentationBuilder.Models;

namespace GMDFAutoDocumentationBuilder.Services;

public sealed class ErrorLogger
{
    private readonly List<FailureRecord> _failures = new();

    public void Record(ModManifestInfo manifest, int attempt, Exception ex)
    {
        _failures.Add(new FailureRecord(
            ModName: manifest.Name,
            UniqueId: manifest.UniqueId,
            DirectoryPath: manifest.DirectoryPath,
            AttemptNumber: attempt,
            ErrorMessage: ex.Message,
            Timestamp: DateTimeOffset.UtcNow));
    }

    public IReadOnlyList<FailureRecord> GetAll() => _failures.AsReadOnly();

    public async Task FlushAsync(string modDirectoryPath)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var logPath = Path.Combine(modDirectoryPath, "gmdf_error_log.txt");
        var content = BuildLogContent(timestamp);

        await File.AppendAllTextAsync(logPath, content).ConfigureAwait(false);
    }

    public void Clear() => _failures.Clear();

    private string BuildLogContent(DateTimeOffset timestamp)
    {
        var builder = new StringBuilder();

        if (_failures.Count == 0)
        {
            builder.AppendLine($"=== GMDF Run — {timestamp:O}: No failures. ===");
            return builder.ToString();
        }

        builder.AppendLine($"=== GMDF Error Log — {timestamp:O} ===");
        foreach (var failure in _failures)
        {
            builder.AppendLine($"[Attempt {failure.AttemptNumber}] {failure.ModName} ({failure.UniqueId}) in {failure.DirectoryPath}");
            builder.AppendLine($"  Error: {failure.ErrorMessage}");
        }

        return builder.ToString();
    }
}

public readonly record struct FailureRecord(
    string ModName,
    string UniqueId,
    string DirectoryPath,
    int AttemptNumber,
    string ErrorMessage,
    DateTimeOffset Timestamp
);
