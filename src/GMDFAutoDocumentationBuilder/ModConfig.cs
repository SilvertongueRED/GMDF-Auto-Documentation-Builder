using StardewModdingAPI;

namespace GMDFAutoDocumentationBuilder;

public sealed class ModConfig
{
    public bool ScanOnLaunch { get; set; } = true;

    public SButton BuildKeybind { get; set; } = SButton.None;

    public string NexusApiKey { get; set; } = string.Empty;
}
