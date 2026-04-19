using GMDFAutoDocumentationBuilder.Services;

namespace GMDFAutoDocumentationBuilder.Tests.Services;

public sealed class ManifestScannerTests
{
    [Fact]
    public void Scan_Finds_Mod_Manifest_And_NexusId()
    {
        var root = CreateTempDirectory();

        try
        {
            var modDir = Directory.CreateDirectory(Path.Combine(root, "TestMod")).FullName;
            File.WriteAllText(Path.Combine(modDir, "manifest.json"), """
            {
              "Name": "Test Mod",
              "Author": "Test Author",
              "Description": "Test Description",
              "Version": "1.2.3",
              "UniqueID": "Author.TestMod",
              "UpdateKeys": ["Nexus:12345"],
              "Dependencies": [{ "UniqueID": "Pathoschild.ContentPatcher" }]
            }
            """);

            var scanner = new ManifestScanner();
            var results = scanner.Scan(root);

            var mod = Assert.Single(results);
            Assert.Equal("Test Mod", mod.Name);
            Assert.Equal(12345, mod.NexusModId);
            Assert.Contains("Pathoschild.ContentPatcher", mod.Dependencies);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(new[] { "Nexus:42" }, 42)]
    [InlineData(new[] { "nexus:9000" }, 9000)]
    [InlineData(new[] { "Github:abc" }, null)]
    [InlineData(new[] { "Nexus:not-a-number" }, null)]
    public void TryGetNexusModId_Parses_Expected_Value(string[] keys, int? expected)
    {
        var actual = ManifestScanner.TryGetNexusModId(keys);
        Assert.Equal(expected, actual);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gmdf-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
