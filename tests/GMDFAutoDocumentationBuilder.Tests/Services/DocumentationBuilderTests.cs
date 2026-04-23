using GMDFAutoDocumentationBuilder.Models;
using GMDFAutoDocumentationBuilder.Services;

namespace GMDFAutoDocumentationBuilder.Tests.Services;

public sealed class DocumentationBuilderTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ModManifestInfo MakeManifest(
        string name = "Local Name",
        string author = "Author",
        string description = "Manifest Description",
        string version = "1.0.0",
        string uniqueId = "Author.LocalName",
        string[]? updateKeys = null,
        string[]? dependencies = null,
        int? nexusModId = 111)
    {
        return new ModManifestInfo(
            DirectoryPath: "/tmp/mod",
            Name: name,
            Author: author,
            Description: description,
            Version: version,
            UniqueId: uniqueId,
            UpdateKeys: updateKeys ?? new[] { "Nexus:111" },
            Dependencies: dependencies ?? new[] { "Dep.One", "Dep.Two" },
            NexusModId: nexusModId);
    }

    private static NexusModInfo MakeNexus(
        string modName = "Nexus Name",
        string summary = "Nexus Summary",
        string[]? features = null,
        string sourceUrl = "https://example.com",
        string? fullDescription = null,
        string? pictureUrl = null)
    {
        return new NexusModInfo(
            ModName: modName,
            Summary: summary,
            Features: features ?? new[] { "Feature A", "Feature B" },
            SourceUrl: sourceUrl,
            FullDescription: fullDescription,
            PictureUrl: pictureUrl);
    }

    // ── Existing baseline test ────────────────────────────────────────────────

    [Fact]
    public void Build_Includes_Metadata_And_Features_In_Expected_Place()
    {
        var manifest = MakeManifest();
        var nexus = MakeNexus();

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

    // ── headerImage ───────────────────────────────────────────────────────────

    [Fact]
    public void Build_Sets_HeaderImage_When_LocalBannerPath_Provided()
    {
        var manifest = MakeManifest();
        var nexus = MakeNexus();

        var doc = new DocumentationBuilder().Build(manifest, nexus, localBannerPath: "assets/gmdf_banner.jpg");

        var overviewPage = doc.Pages[0];
        Assert.NotNull(overviewPage.HeaderImage);
        Assert.Equal("assets/gmdf_banner.jpg", overviewPage.HeaderImage!.Texture);
    }

    [Fact]
    public void Build_Omits_HeaderImage_When_No_BannerPath()
    {
        var manifest = MakeManifest();
        var nexus = MakeNexus();

        var doc = new DocumentationBuilder().Build(manifest, nexus);

        Assert.Null(doc.Pages[0].HeaderImage);
    }

    // ── Description page ─────────────────────────────────────────────────────

    [Fact]
    public void Build_Adds_Description_Page_When_FullDescription_Is_Substantial()
    {
        var manifest = MakeManifest();
        // Supply a full description much longer than the summary so a second page is generated.
        var longDesc = "Overview\n\nThis mod does many wonderful things for you.\n\n" +
                       "Installation\n\nDrop the files into your Mods folder.\n\n" +
                       "Configuration\n\nOpen config.json to adjust settings.";
        var nexus = MakeNexus(summary: "Short summary.", fullDescription: longDesc);

        var doc = new DocumentationBuilder().Build(manifest, nexus);

        Assert.Equal(2, doc.Pages.Count);
        Assert.Equal("Overview", doc.Pages[0].Name);
        Assert.Equal("Description", doc.Pages[1].Name);
    }

    [Fact]
    public void Build_Omits_Description_Page_When_FullDescription_Is_Absent()
    {
        var manifest = MakeManifest();
        var nexus = MakeNexus(fullDescription: null);

        var doc = new DocumentationBuilder().Build(manifest, nexus);

        Assert.Single(doc.Pages);
    }

    [Fact]
    public void Build_Omits_Description_Page_When_FullDescription_Is_Not_Substantially_Longer_Than_Summary()
    {
        var summary = "This mod does things.";
        var nexus = MakeNexus(summary: summary, fullDescription: summary + " And a bit more.");

        var doc = new DocumentationBuilder().Build(MakeManifest(), nexus);

        Assert.Single(doc.Pages);
    }

    // ── Description page entry-type detection ─────────────────────────────────

    [Fact]
    public void Build_Description_Page_Detects_Section_Titles()
    {
        var desc =
            "Overview\n\n" +
            "This is the overview text of the mod. It explains what the mod does.\n\n" +
            "Installation\n\n" +
            "Follow these steps to install the mod correctly.";
        var nexus = MakeNexus(summary: "Short.", fullDescription: desc);

        var doc = new DocumentationBuilder().Build(MakeManifest(), nexus);

        var descPage = doc.Pages[1];
        Assert.Contains(descPage.Entries, e => e is SectionTitleEntry s && s.Text == "Overview");
        Assert.Contains(descPage.Entries, e => e is SectionTitleEntry s && s.Text == "Installation");
    }

    [Fact]
    public void Build_Description_Page_Detects_Unordered_Lists()
    {
        var desc =
            "Features of this mod include several improvements to the game.\n\n" +
            "- Alpha feature which does something useful\n" +
            "- Beta feature which does something else\n" +
            "- Gamma feature which also adds value";
        var nexus = MakeNexus(summary: "Short.", fullDescription: desc);

        var doc = new DocumentationBuilder().Build(MakeManifest(), nexus);

        var descPage = doc.Pages[1];
        Assert.Contains(descPage.Entries, e => e is ListEntry l &&
            l.Items.Contains("Alpha feature which does something useful") &&
            l.Items.Contains("Beta feature which does something else") &&
            l.Items.Contains("Gamma feature which also adds value"));
    }

    [Fact]
    public void Build_Description_Page_Detects_Ordered_Lists()
    {
        var desc =
            "Steps to install this mod correctly and enjoy its features.\n\n" +
            "1. Download the mod from Nexus Mods\n" +
            "2. Place the folder inside your Mods directory\n" +
            "3. Launch the game and verify the mod is active";
        var nexus = MakeNexus(summary: "Short.", fullDescription: desc);

        var doc = new DocumentationBuilder().Build(MakeManifest(), nexus);

        var descPage = doc.Pages[1];
        Assert.Contains(descPage.Entries, e => e is OrderedListEntry o &&
            o.Items[0] == "Download the mod from Nexus Mods" &&
            o.Items[1] == "Place the folder inside your Mods directory");
    }

    // ── New entry-type records ────────────────────────────────────────────────

    [Fact]
    public void OrderedListEntry_Has_Correct_Type()
    {
        var entry = new OrderedListEntry(new[] { "one", "two" });
        Assert.Equal("orderedList", entry.Type);
        Assert.Equal(2, entry.Items.Count);
    }

    [Fact]
    public void CaptionEntry_Has_Correct_Type()
    {
        var entry = new CaptionEntry("Figure 1");
        Assert.Equal("caption", entry.Type);
        Assert.Equal("Figure 1", entry.Text);
    }

    [Fact]
    public void SpacerEntry_Has_Correct_Type_And_Default_Height()
    {
        var entry = new SpacerEntry();
        Assert.Equal("spacer", entry.Type);
        Assert.Equal(16, entry.Height);
    }

    [Fact]
    public void SpoilerEntry_Has_Correct_Type()
    {
        var entry = new SpoilerEntry("Click me", "Hidden content");
        Assert.Equal("spoiler", entry.Type);
        Assert.Equal("Click me", entry.Label);
        Assert.Equal("Hidden content", entry.Text);
    }

    [Fact]
    public void ImageEntry_Has_Correct_Type()
    {
        var entry = new ImageEntry("assets/screenshot.png");
        Assert.Equal("image", entry.Type);
        Assert.Equal("assets/screenshot.png", entry.Texture);
    }

    [Fact]
    public void GifEntry_Has_Correct_Type()
    {
        var entry = new GifEntry("assets/anim.png", FrameCount: 8, FrameDuration: 0.1);
        Assert.Equal("gif", entry.Type);
        Assert.Equal(8, entry.FrameCount);
    }

    [Fact]
    public void RowEntry_Has_Correct_Type()
    {
        var left = new List<object> { new ParagraphEntry("left") };
        var right = new List<object> { new ParagraphEntry("right") };
        var entry = new RowEntry(left, right);
        Assert.Equal("row", entry.Type);
    }

    [Fact]
    public void IndentBlockEntry_Has_Correct_Type()
    {
        var children = new List<object> { new ParagraphEntry("child") };
        var entry = new IndentBlockEntry(children);
        Assert.Equal("indentBlock", entry.Type);
    }

    // ── NexusModInfo new fields ───────────────────────────────────────────────

    [Fact]
    public void NexusModInfo_Supports_FullDescription_And_PictureUrl()
    {
        var nexus = new NexusModInfo(
            ModName: "Mod",
            Summary: "Short",
            Features: Array.Empty<string>(),
            SourceUrl: null,
            FullDescription: "Full text",
            PictureUrl: "https://cdn.nexus.com/pic.jpg");

        Assert.Equal("Full text", nexus.FullDescription);
        Assert.Equal("https://cdn.nexus.com/pic.jpg", nexus.PictureUrl);
    }

    [Fact]
    public void NexusModInfo_FullDescription_And_PictureUrl_Default_To_Null()
    {
        var nexus = new NexusModInfo(
            ModName: "Mod",
            Summary: "Short",
            Features: Array.Empty<string>(),
            SourceUrl: null);

        Assert.Null(nexus.FullDescription);
        Assert.Null(nexus.PictureUrl);
    }
}

