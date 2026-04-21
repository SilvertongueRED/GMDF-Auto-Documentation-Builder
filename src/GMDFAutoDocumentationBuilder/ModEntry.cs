using System.Text.Json;
using GMDFAutoDocumentationBuilder.Models;
using GMDFAutoDocumentationBuilder.Services;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace GMDFAutoDocumentationBuilder;

public sealed class ModEntry : Mod
{
    private readonly ManifestScanner _scanner = new();
    private readonly NexusMetadataProvider _nexus = new();
    private readonly DocumentationBuilder _documentationBuilder = new();
    private readonly ErrorLogger _errorLogger = new();

    private ModConfig _config = new();
    private bool _generationInProgress;

    public override void Entry(IModHelper helper)
    {
        _config = helper.ReadConfig<ModConfig>();
        helper.WriteConfig(_config);

        if (string.IsNullOrWhiteSpace(_config.NexusApiKey))
        {
            var configPath = Path.Combine(Helper.DirectoryPath, "config.json");
            Monitor.Log(
                $"[GMDF] No Nexus API key is configured. Open your config.json at:{Environment.NewLine}  {configPath}{Environment.NewLine}and set the NexusApiKey field to enable full Nexus metadata fetching.",
                LogLevel.Alert);
        }

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
        _errorLogger.Clear();

        var modsDirectory = ResolveModsDirectory();
        var manifests = _scanner.Scan(modsDirectory);
        Monitor.Log($"Discovered {manifests.Count} mod manifest(s) in '{modsDirectory}'.", LogLevel.Info);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var manifestsToProcess = manifests
            .Where(manifest => !string.Equals(manifest.UniqueId, ModManifest.UniqueID, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var successfulCount = 0;
        var failedManifests = new List<ModManifestInfo>();

        foreach (var manifest in manifestsToProcess)
        {
            var succeeded = await TryGenerateDocumentationAsync(manifest, options, attempt: 1).ConfigureAwait(false);
            if (succeeded)
                successfulCount++;
            else
                failedManifests.Add(manifest);
        }

        if (failedManifests.Count > 0 && _config.RescanRetryCount > 0)
        {
            for (var attempt = 2; attempt <= _config.RescanRetryCount + 1 && failedManifests.Count > 0; attempt++)
            {
                Monitor.Log($"Rescanning {failedManifests.Count} previously failed mod(s) (attempt {attempt})...", LogLevel.Info);

                var stillFailing = new List<ModManifestInfo>();
                foreach (var manifest in failedManifests)
                {
                    var succeeded = await TryGenerateDocumentationAsync(manifest, options, attempt).ConfigureAwait(false);
                    if (succeeded)
                        successfulCount++;
                    else
                        stillFailing.Add(manifest);
                }

                failedManifests = stillFailing;
            }
        }

        if (_config.EnableErrorLog)
            await _errorLogger.FlushAsync(Helper.DirectoryPath).ConfigureAwait(false);

        Monitor.Log($"GMDF documentation generation complete. Succeeded: {successfulCount}, Failed: {failedManifests.Count}.", LogLevel.Info);
    }

    private async Task<bool> TryGenerateDocumentationAsync(ModManifestInfo manifest, JsonSerializerOptions options, int attempt)
    {
        try
        {
            var nexus = await _nexus.FetchAsync(manifest, _config.NexusApiKey, Monitor, CancellationToken.None).ConfigureAwait(false);
            var document = _documentationBuilder.Build(manifest, nexus);

            var outputPath = Path.Combine(manifest.DirectoryPath, "documentation.json");
            var outputJson = JsonSerializer.Serialize(document, options);
            await File.WriteAllTextAsync(outputPath, outputJson).ConfigureAwait(false);

            Monitor.Log($"Generated documentation for '{manifest.Name}' at {outputPath}.", LogLevel.Trace);
            return true;
        }
        catch (Exception ex)
        {
            _errorLogger.Record(manifest, attempt, ex);
            Monitor.Log($"Failed to generate documentation for '{manifest.Name}' in '{manifest.DirectoryPath}' (attempt {attempt}): {ex.Message}", LogLevel.Warn);
            return false;
        }
    }

    private string ResolveModsDirectory()
    {
        var ownDirectory = Helper.DirectoryPath;
        var parentDirectory = Directory.GetParent(ownDirectory)?.FullName;

        if (!string.IsNullOrWhiteSpace(parentDirectory) && Directory.Exists(parentDirectory))
            return parentDirectory;

        return Path.Combine(Constants.GamePath, "Mods");
    }
}
