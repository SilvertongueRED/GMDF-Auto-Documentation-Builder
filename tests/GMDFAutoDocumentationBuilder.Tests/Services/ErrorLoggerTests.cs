using GMDFAutoDocumentationBuilder.Models;
using GMDFAutoDocumentationBuilder.Services;

namespace GMDFAutoDocumentationBuilder.Tests.Services;

public sealed class ErrorLoggerTests
{
    [Fact]
    public async Task FlushAsync_Appends_Failure_And_NoFailure_Runs()
    {
        var root = CreateTempDirectory();

        try
        {
            var manifest = new ModManifestInfo(
                DirectoryPath: "/mods/TestMod",
                Name: "Test Mod",
                Author: "Author",
                Description: "Description",
                Version: "1.0.0",
                UniqueId: "Author.TestMod",
                UpdateKeys: Array.Empty<string>(),
                Dependencies: Array.Empty<string>(),
                NexusModId: null);

            var logger = new ErrorLogger();
            logger.Record(manifest, attempt: 1, new InvalidOperationException("boom"));

            await logger.FlushAsync(root);
            logger.Clear();
            await logger.FlushAsync(root);

            var logPath = Path.Combine(root, "gmdf_error_log.txt");
            var content = await File.ReadAllTextAsync(logPath);

            Assert.Contains("=== GMDF Error Log —", content);
            Assert.Contains("[Attempt 1] Test Mod (Author.TestMod) in /mods/TestMod", content);
            Assert.Contains("Error: boom", content);
            Assert.Contains("No failures.", content);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Clear_Removes_Recorded_Failures()
    {
        var manifest = new ModManifestInfo(
            DirectoryPath: "/mods/TestMod",
            Name: "Test Mod",
            Author: "Author",
            Description: "Description",
            Version: "1.0.0",
            UniqueId: "Author.TestMod",
            UpdateKeys: Array.Empty<string>(),
            Dependencies: Array.Empty<string>(),
            NexusModId: null);

        var logger = new ErrorLogger();
        logger.Record(manifest, attempt: 1, new InvalidOperationException("boom"));

        Assert.Single(logger.GetAll());

        logger.Clear();

        Assert.Empty(logger.GetAll());
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gmdf-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
