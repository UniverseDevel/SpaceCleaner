# 🧹 SpaceCleaner

A Windows desktop app that **finds and reports** temporary files, caches, and other safe-to-remove
clutter across your drives — and explains *why* each location is safe and what it contains.

> **Reporting only.** SpaceCleaner does **not** delete anything (yet). It scans, measures, and shows
> you where the reclaimable space is. A guided removal step is planned for a future version.

---

## Why another cleaner?

Most "junk cleaners" either hide what they're doing or aggressively delete things based on guesswork.
SpaceCleaner takes the opposite approach:

- **Curated, not heuristic.** It only looks at a hand-picked catalog of ~45 well-known temp/cache
  locations. Every result traces back to a category with a written *"why this is safe"* and
  *"what it usually contains"* explanation — so you always understand what you're looking at.
- **Safe by design.** It never touches the live OS, installed program binaries, the registry, or
  cloud-synced files (see [Safety](#safety)).
- **Transparent sizes.** Results form a tree where every folder's size rolls up into its parents, so a
  drive node shows the *total* reclaimable space at a glance.

## Features

- ✅ **Per-drive scanning** — pick which drives to scan; each runs on its own thread with its own
  live progress bar.
- 🌳 **Roll-up size tree** — `Drive → Category → folders`, sorted largest-first, sizes accumulating
  into every parent.
- 💡 **Plain-language explanations** — click any item to see why it's safe, what it typically holds,
  and any caution (e.g. "will be re-downloaded on next build").
- 📂 **Open in Explorer** — jump straight to any reported folder.
- 👥 **All user profiles** — optionally scan every account under `C:\Users`, not just the current one.
- 🟢 **Safety ratings** — every location is rated **Safe** (regenerates, no downside), **Caution**
  (fine, but re-downloads/rebuilds), or **Review** (may hold wanted data or affect rollback). Shown as
  a colored dot per row and a badge in the details pane.
- ☑️ **Selection + quick presets** — tri-state checkboxes plus one-click toggles:
  - **All** · **Temp & Logs**
  - by safety: **Safe** · **Caution** · **Review**
  - by re-download: **Redownloadable** · **Non-redownloadable**
  - *(selection is groundwork for the upcoming removal step)*

## What it scans

A broad, curated catalog including:

- **Windows:** Recycle Bin, user & system `%TEMP%`, Windows Update download cache, Delivery
  Optimization, component logs (CBS/DISM), Prefetch, thumbnail/icon cache, Internet (WinINet) cache,
  Windows Error Reporting, `Windows.old` and upgrade leftovers, `PerfLogs`.
- **GPU / drivers:** NVIDIA / AMD / Intel shader caches, generic DirectX shader cache, NVIDIA installer
  cache, and extracted driver folders at drive roots (`C:\NVIDIA`, `C:\AMD`, …).
- **Browsers:** Chrome, Edge, Brave, Vivaldi, Opera, Firefox caches.
- **IDEs / dev tools:** JetBrains (IntelliJ/Rider/PyCharm), Visual Studio (installer & component
  caches), VS Code; package-manager caches for NuGet, npm, pnpm, Yarn, pip, Gradle, Maven, Cargo, Go,
  Composer.
- **Apps:** Discord, Slack, Teams, Spotify, Zoom, Adobe media cache.

## Safety

SpaceCleaner is conservative by default:

- **No deletion** — current versions only report.
- **No cloud downloads** — cloud-synced folders (OneDrive, Dropbox, Google Drive, …) and any
  placeholder files (`Offline` / `RecallOnOpen` / `RecallOnDataAccess` attributes) are skipped, so the
  scan never triggers a download. Reparse points / junctions are skipped too.
- **System-critical folders are off-limits** — `C:\Windows`, `Program Files`, `ProgramData`, etc. are
  excluded. A small, explicit **allow-list** lets the scanner reach *only* specific known-safe folders
  inside them (e.g. `C:\Windows\Temp`) while everything else stays protected.
- **Fast, fault-tolerant enumeration** — inaccessible entries are skipped instead of crashing the scan.

## Requirements

- Windows 10 / 11 (x64)
- [.NET 9 SDK](https://dotnet.microsoft.com/download) to build from source
- **Administrator rights** — the app requests elevation on launch (UAC) so it can read Recycle Bins,
  other users' caches, `Windows.old`, and protected temp folders.

## Build & run

```powershell
# Clone, then from the project folder:
dotnet build

# Run from an ELEVATED terminal (the app requires admin):
dotnet run
```

Then choose your drives and press **Scan**.

### Standalone executable

To produce a self-contained `.exe` that runs without .NET installed (recommended if .NET is only
installed per-user, which an elevated process can't see):

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The result lands in `bin/Release/net9.0-windows/win-x64/publish/SpaceCleaner.exe`.

## Tech

- **.NET 9**, **WPF** (MVVM)
- Multi-threaded scanning with cancellation and live progress
- No third-party runtime dependencies

## Roadmap

- [ ] Guided removal of selected locations (with confirmation and dry-run)
- [ ] Persist drive/preset choices between runs
- [ ] Export report (CSV / JSON)

## License

See [LICENSE](LICENSE) if present; otherwise all rights reserved by the author.
