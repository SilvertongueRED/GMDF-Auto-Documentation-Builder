using GMDFAutoDocumentationBuilder.Models;
using GMDFAutoDocumentationBuilder.Services;

namespace GMDFAutoDocumentationBuilder.Tests.Services;

public sealed class DocumentationBuilderTests
{
    [Fact]
    public void Build_Includes_Metadata_And_Features_In_Expected_Place()
    {
        var manifest = new ModManifestInfo(
            DirectoryPath: "/tmp/mod",
            Name: "Local Name",
            Author: "Author",
            Description: "Manifest Description",
            Version: "1.0.0",
            UniqueId: "Author.LocalName",
            UpdateKeys: new[] { "Nexus:111" },
            Dependencies: new[] { "Dep.One", "Dep.Two" },
            NexusModId: 111);

        var nexus = new NexusModInfo(
            ModName: "Nexus Name",
            Summary: "Nexus Summary",
            Features: new[] { "Feature A", "Feature B" },
            SourceUrl: "https://example.com");

        var builder = new DocumentationBuilder();
        var doc = builder.Build(manifest, nexus);

        Assert.Equal(1, doc.Format);
        Assert.Equal("Nexus Name", doc.ModName);

        var page = Assert.Single(doc.Pages);
        Assert.Equal("Overview", page.Name);
        Assert.Contains(page.Entries, x => x is KeyValueEntry k && k.Key == "Author" && k.Value == "Author");
        Assert.Contains(page.Entries, x => x is KeyValueEntry k && k.Key == "Nexus ID" && k.Value == "111");
        Assert.Contains(page.Entries, x => x is ListEntry l && l.Items.Contains("Feature A"));
    }
}
