using System.Text.Json;
using GMDFAutoDocumentationBuilder.Services;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace GMDFAutoDocumentationBuilder;

public sealed class ModEntry : Mod
{
    private readonly ManifestScanner _scanner = new();
    private readonly NexusMetadataProvider _nexus = new();
    private readonly DocumentationBuilder _documentationBuilder = new();

    private ModConfig _config = new();
    private bool _generationInProgress;

    public override void Entry(IModHelper helper)
    {
        Helper = helper;
        _config = helper.ReadConfig<ModConfig>();

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.ConsoleCommands.Add(
            "gmdf_build_docs",
            "Generate documentation.json files for all discovered mods.",
            (_, _) => TriggerGeneration("console command"));
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        if (_config.ScanOnLaunch)
            TriggerGeneration("launch scan");
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (_config.BuildKeybind is SButton.None)
            return;

        if (e.Button == _config.BuildKeybind)
            TriggerGeneration("keybind");
    }

    private void TriggerGeneration(string reason)
    {
        if (_generationInProgress)
        {
            Monitor.Log("Documentation generation is already running.", LogLevel.Info);
            return;
        }

        _generationInProgress = true;
        _ = Task.Run(async () =>
        {
            try
            {
                Monitor.Log($"Starting GMDF documentation build ({reason}).", LogLevel.Info);
                await GenerateDocumentationFilesAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Documentation generation failed: {ex}", LogLevel.Error);
            }
            finally
            {
                _generationInProgress = false;
            }
        });
    }

    private async Task GenerateDocumentationFilesAsync()
    {
        var modsDirectory = ResolveModsDirectory();
        var manifests = _scanner.Scan(modsDirectory);
        Monitor.Log($"Discovered {manifests.Count} mod manifest(s) in '{modsDirectory}'.", LogLevel.Info);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        foreach (var manifest in manifests)
        {
            if (string.Equals(manifest.UniqueId, ModManifest.UniqueID, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var nexus = await _nexus.FetchAsync(manifest, _config.NexusApiKey, Monitor, CancellationToken.None).ConfigureAwait(false);
                var document = _documentationBuilder.Build(manifest, nexus);

                var outputPath = Path.Combine(manifest.DirectoryPath, "documentation.json");
                var outputJson = JsonSerializer.Serialize(document, options);
                await File.WriteAllTextAsync(outputPath, outputJson).ConfigureAwait(false);

                Monitor.Log($"Generated documentation for '{manifest.Name}' at {outputPath}.", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to generate documentation for '{manifest.Name}' in '{manifest.DirectoryPath}': {ex.Message}", LogLevel.Warn);
            }
        }

        Monitor.Log("GMDF documentation generation complete.", LogLevel.Info);
    }

    private string ResolveModsDirectory()
    {
        var ownDirectory = Helper.DirectoryPath;
        var parentDirectory = Directory.GetParent(ownDirectory)?.FullName;

        if (!string.IsNullOrWhiteSpace(parentDirectory) && Directory.Exists(parentDirectory))
            return parentDirectory;

        return Path.Combine(Constants.ExecutionPath, "Mods");
    }
}
