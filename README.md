# GMDF-Auto-Documentation-Builder

A Stardew Valley SMAPI mod that scans installed mods and generates GMDF-compatible `documentation.json` files.

## Features
- Scans `Mods` and `Mods/Stardrop Installed Mods` for `manifest.json` files.
- Reads manifest fields (`Name`, `Author`, `Description`, `Version`, `UniqueID`, `UpdateKeys`, `Dependencies`).
- Uses Nexus API (`Nexus:<id>` update keys) when `NexusApiKey` is configured.
- Falls back to scraping the public Nexus description page when no API key is available.
- Writes a GMDF format v1 `documentation.json` into each mod folder.

## Config (`config.json`)
```json
{
  "ScanOnLaunch": true,
  "BuildKeybind": "None",
  "NexusApiKey": "",
  "RescanRetryCount": 1,
  "EnableErrorLog": true
}
```

## Triggering generation
- Automatically on launch when `ScanOnLaunch` is `true`.
- Press the configured `BuildKeybind`.
- Run SMAPI command: `gmdf_build_docs`.
