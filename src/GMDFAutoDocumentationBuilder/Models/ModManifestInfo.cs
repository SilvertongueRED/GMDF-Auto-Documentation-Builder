namespace GMDFAutoDocumentationBuilder.Models;

public sealed record ModManifestInfo(
    string DirectoryPath,
    string Name,
    string Author,
    string Description,
    string Version,
    string UniqueId,
    IReadOnlyList<string> UpdateKeys,
    IReadOnlyList<string> Dependencies,
    int? NexusModId
);
