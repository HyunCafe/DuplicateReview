# Duplicate Review v0.1.2

First public release of a small, standalone duplicate-review extension for Playnite 10 Desktop.

### Features

- Review exact duplicate titles across Epic, GOG, Steam, Amazon Games, EA, and local libraries.
- **Possible matches** identifies punctuation, article, edition-label, and limited typo variations for manual review.
- Separate **Duplicates**, **Possible matches**, and **Hidden games** tabs.
- Highlight multiple entries with Ctrl/Shift/Ctrl+A, then bulk mark/unmark.
- Preview and confirm Hide/Restore; no automatic hiding or game deletion.
- Preserve the last visible copy of a title and protect installed copies.
- No network, filesystem scan, telemetry, or background automation in the reviewed source.

### Installation

Download `DuplicateReview-0.1.2.pext`, verify its SHA-256 against `SHA256SUMS.txt`, open it in Playnite, and restart Playnite. Open **Main menu → Extensions → Duplicate Review → Review duplicate game titles...**.

### Validation

The tested v0.1.2 development package passed the existing PowerShell regression tests, a Windows .NET Release build, packaging, and user-performed Playnite checks of similar-title detection, batch marking, and hiding. Live testing is not exhaustive.

### Security

Playnite plugins are native .NET code loaded into Playnite and **are not sandboxed**. Review source and package integrity, and back up the library before bulk changes.

Source: https://github.com/HyunCafe/DuplicateReview
License: MIT
