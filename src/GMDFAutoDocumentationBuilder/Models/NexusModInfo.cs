namespace GMDFAutoDocumentationBuilder.Models;

public sealed record NexusModInfo(
    string? ModName,
    string? Summary,
    IReadOnlyList<string> Features,
    string? SourceUrl,
    string? FullDescription = null,
    string? PictureUrl = null
);
