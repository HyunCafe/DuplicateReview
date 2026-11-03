# Security policy

Duplicate Review is a Playnite .NET extension. **Playnite extensions run with the permissions of the Playnite application and are not sandboxed.** Never treat drag-and-drop installation as a security boundary. Do not run Playnite as administrator for this plugin.

## Intended scope

Duplicate Review reads game records only when the review window is opened or refreshed, and changes the `Game.Hidden` property only after an explicit user confirmation. There are no network or telemetry features, filesystem scans, startup jobs, process launching, or game deletion functions in the reviewed v0.1.2 source.

The release `.pext` contains only `DuplicateReview.dll` and `extension.yaml`, with no embedded third-party runtime assemblies. Public releases include `SHA256SUMS.txt` for package integrity. This checksum validates only the downloadable asset, **not whether the code is risk-free**.

## Before installing

- Obtain the package from the official HyunCafe GitHub release, not a mirror.
- Verify SHA-256 of the package against the release checksum.
- Back up your Playnite library prior to batch hide/restore actions.
- Treat future versions as separate executable-code trust decisions.

## Reporting vulnerabilities

Avoid sharing private data, passwords, keys, or full local library inventories in public issues. Use GitHub private vulnerability reporting if enabled; otherwise open a minimal public issue requesting a private channel.

## Supported versions

Only the latest published release is intended to receive maintenance. No guaranteed response time is offered.
