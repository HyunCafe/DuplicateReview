# Duplicate Review for Playnite

A small, standalone **manual duplicate-game review extension** for Playnite 10 Desktop. Published by **HyunCafe**.

Duplicate Review identifies games that appear more than once across stores and local installations. It opens a separate window so you can review candidates, select multiple rows, and hide unwanted library entries without deleting games, ownership records, metadata, or playtime.

## Features

- **Duplicates** — finds two or more visible entries with the same name, ignoring case and repeated whitespace.
- **Possible matches** — suggests differences in punctuation, leading articles, edition labels, and small spelling errors. **Suggestions may be different games or editions.** Nothing is hidden automatically.
- **Hidden games** — view and restore hidden copies that have matching or similar titles. Entries hidden outside this plugin can appear here too.
- **Ctrl-click / Shift-click / Ctrl+A**, followed by **Mark highlighted** or **Unmark highlighted**, for safe batch selection. Row highlighting alone never changes your library.
- **Hide marked...** or **Restore marked...** only after an explicit confirmation preview.
- Protects the last visible copy of a matching title and the last visible installed copy when one exists.
- Shows title, store/source, installed status, hidden state, and platform. Refresh re-reads the current Playnite database on demand.

## Installation

1. Back up your Playnite library before bulk changes.
2. Download [`DuplicateReview-0.1.2.pext`](https://github.com/HyunCafe/DuplicateReview/releases/tag/v0.1.2) from the GitHub release, and compare its SHA-256 to `SHA256SUMS.txt`.
3. Open the `.pext` in Playnite Desktop and restart Playnite.
4. Open **Main menu → Extensions → Duplicate Review → Review duplicate game titles...**.
5. Review suggested duplicates, mark only the entries you wish to hide, and confirm the preview.

## Security and privacy

- The package contains **only** `DuplicateReview.dll` and `extension.yaml`; it bundles no additional third-party DLLs.
- The extension has no network, telemetry, startup scan, filesystem scan, automatic hiding, process launching, or plugin-created settings files in its reviewed source.
- The intended library write is Playnite's `Game.Hidden` property after confirmation. No library records or game files are deleted.
- **Playnite plugins are not sandboxed.** A plugin runs with Playnite's Windows-user permissions. Review its code and provenance before installing any version.
- The source is published under the MIT license. See [SECURITY.md](SECURITY.md).

## Build from source

On Windows with PowerShell 7 and the .NET SDK:

```powershell
dotnet restore .\DuplicateReview.csproj --source https://api.nuget.org/v3/index.json
pwsh -NoLogo -NoProfile -File .\Tests\Run-Tests.ps1
dotnet build .\DuplicateReview.csproj -c Release --no-restore -p:DebugType=none -p:DebugSymbols=false
pwsh -NoLogo -NoProfile -File .\Tools\Package-Release.ps1 -OutputName DuplicateReview-0.1.2-local.pext
```

Build dependencies: `PlayniteSDK` 6.18.0 and `Microsoft.NETFramework.ReferenceAssemblies.net462` 1.0.3. The plugin targets .NET Framework 4.6.2. Locally built ZIP bytes and compiled DLLs can differ across environments.

## Limitations

- Similar-title suggestions are heuristic. *Skyrim* and *Skyrim Special Edition*, for example, should be reviewed as distinct editions.
- Similarity matching can miss some variants, and exact titles can be unrelated games. No online game ID database is consulted.
- Hidden entries with no other matching or similar title are omitted from the Hidden games tab.
- There is no plugin-specific undo ledger: Hidden games may include changes made by Playnite or other extensions.
- If a batch fails midway, earlier successful updates are not automatically rolled back.
- Automated tests cover the matching logic and selected safety gates, not every WPF/Playnite configuration or all malicious-code possibilities.

## Project

- Publisher: **HyunCafe**
- Latest initial public release: **v0.1.2**
- [Changes](CHANGELOG.md) · [MIT License](LICENSE) · [Security policy](SECURITY.md)
