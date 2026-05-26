namespace SpaceCleaner.Models;

/// <summary>
/// The curated set of locations SpaceCleaner reports. Every entry has a concrete, defensible reason
/// for being safe to clear. System-critical areas (the live OS, installed program binaries, registry
/// hives) are never listed; the few entries under <c>C:\Windows</c> / <c>ProgramData</c> point only
/// at well-known throwaway folders that <see cref="Services.SafetyGuard"/> explicitly allow-lists.
/// </summary>
public static class CleanupCatalog
{
    public static IReadOnlyList<CleanupCategory> All { get; } = new[]
    {
        // ============================ Per-drive ============================
        new CleanupCategory
        {
            Id = "recycle-bin",
            Name = "Recycle Bin",
            Kind = CategoryKind.RecycleBin,
            Scope = CategoryScope.PerDrive,
            PathTemplates = new[] { "{DRIVE}$Recycle.Bin" },
            WhySafe = "Files you have already deleted. Windows keeps them on each drive only so you " +
                      "can restore them; they keep occupying space until the Recycle Bin is emptied.",
            TypicalContents = "Deleted files and folders, organized in a subfolder per user account.",
            Caution = "These are files you chose to delete. Make sure nothing here is still needed " +
                      "before emptying the Recycle Bin.",
        },
        new CleanupCategory
        {
            Id = "drive-temp",
            Name = "Drive Temp Folder",
            Kind = CategoryKind.TempFiles,
            Scope = CategoryScope.PerDrive,
            PathTemplates = new[] { "{DRIVE}Temp", "{DRIVE}Tmp" },
            WhySafe = "A general-purpose scratch folder at the root of the drive. Tools and installers " +
                      "drop working files here and rarely clean up after themselves.",
            TypicalContents = "Extracted archives, installer payloads, build scratch files, stray logs.",
        },
        new CleanupCategory
        {
            Id = "windows-old",
            Name = "Previous Windows Installation (Windows.old)",
            Kind = CategoryKind.UpgradeLeftovers,
            Scope = CategoryScope.PerDrive,
            PathTemplates = new[] { "{DRIVE}Windows.old" },
            WhySafe = "A backup of your previous Windows version kept after a feature update or upgrade. " +
                      "It exists only to let you roll back, and Windows deletes it automatically after " +
                      "about 10 days.",
            TypicalContents = "Your old Windows, Program Files and user files from before the upgrade.",
            Caution = "Removing this prevents rolling back to the previous Windows version. Only relevant " +
                      "shortly after a Windows upgrade.",
        },
        new CleanupCategory
        {
            Id = "windows-upgrade-leftovers",
            Name = "Windows Upgrade/Setup Leftovers",
            Kind = CategoryKind.UpgradeLeftovers,
            Scope = CategoryScope.PerDrive,
            PathTemplates = new[] { "{DRIVE}$WINDOWS.~BT", "{DRIVE}$Windows.~WS", "{DRIVE}$GetCurrent", "{DRIVE}ESD" },
            WhySafe = "Temporary working folders created while installing or upgrading Windows. They are " +
                      "abandoned once setup finishes.",
            TypicalContents = "Extracted installation images and setup working files.",
            Caution = "Relevant only right after a Windows upgrade; removing them stops an in-progress " +
                      "upgrade from resuming.",
        },
        new CleanupCategory
        {
            Id = "vendor-driver-extracts",
            Name = "Extracted Driver/Installer Files",
            Kind = CategoryKind.Installer,
            Scope = CategoryScope.PerDrive,
            PathTemplates = new[]
            {
                "{DRIVE}NVIDIA", "{DRIVE}AMD", "{DRIVE}Intel", "{DRIVE}Drivers",
                "{DRIVE}SWSetup", "{DRIVE}Dell", "{DRIVE}OneDriveTemp",
            },
            WhySafe = "Hardware driver and vendor-tool installers (NVIDIA, AMD, Intel, OEM utilities) " +
                      "extract their payload to a folder at the drive root and leave it behind after " +
                      "installation finishes.",
            TypicalContents = "Unpacked driver packages, setup files and installer logs.",
        },
        new CleanupCategory
        {
            Id = "perflogs",
            Name = "Performance Logs",
            Kind = CategoryKind.Logs,
            Scope = CategoryScope.PerDrive,
            PathTemplates = new[] { "{DRIVE}PerfLogs" },
            WhySafe = "Output from Windows performance/diagnostic data collectors. Usually empty and only " +
                      "useful while actively investigating a performance trace.",
            TypicalContents = "Performance counter logs and diagnostic reports.",
        },

        // ============================ Machine-wide ============================
        new CleanupCategory
        {
            Id = "windows-temp",
            Name = "Windows Temp Folder",
            Kind = CategoryKind.TempFiles,
            Scope = CategoryScope.Machine,
            PathTemplates = new[] { "{WINDIR}\\Temp" },
            WhySafe = "The system-wide temporary folder. Windows services and installers use it for " +
                      "short-lived working files that are not expected to survive.",
            TypicalContents = "Service scratch files, installer leftovers, temporary logs.",
        },
        new CleanupCategory
        {
            Id = "windows-update-cache",
            Name = "Windows Update Download Cache",
            Kind = CategoryKind.Cache,
            Redownloads = true,
            Scope = CategoryScope.Machine,
            PathTemplates = new[] { "{WINDIR}\\SoftwareDistribution\\Download" },
            WhySafe = "Update packages Windows has already downloaded. Once an update is installed the " +
                      "files are no longer needed, and Windows re-downloads anything it still requires.",
            TypicalContents = "Downloaded Windows Update payloads and manifests.",
            Caution = "Pending (not-yet-installed) updates would need to be downloaded again.",
        },
        new CleanupCategory
        {
            Id = "delivery-optimization",
            Name = "Delivery Optimization Files",
            Kind = CategoryKind.Cache,
            Redownloads = true,
            Scope = CategoryScope.Machine,
            PathTemplates = new[] { "{WINDIR}\\SoftwareDistribution\\DeliveryOptimization" },
            WhySafe = "Cached update/app content Windows keeps to share on the local network. It is purely " +
                      "a cache and is rebuilt on demand.",
            TypicalContents = "Cached Delivery Optimization content fragments.",
        },
        new CleanupCategory
        {
            Id = "windows-logs",
            Name = "Windows Component Logs",
            Kind = CategoryKind.Logs,
            Scope = CategoryScope.Machine,
            PathTemplates = new[] { "{WINDIR}\\Logs" },
            WhySafe = "Diagnostic logs written by Windows servicing components (CBS, DISM, setup). They are " +
                      "only useful when troubleshooting a specific servicing problem.",
            TypicalContents = "CBS, DISM and setup log files.",
            Caution = "Useful if you are currently diagnosing a Windows update or servicing issue.",
        },
        new CleanupCategory
        {
            Id = "windows-prefetch",
            Name = "Windows Prefetch",
            Kind = CategoryKind.Cache,
            Scope = CategoryScope.Machine,
            PathTemplates = new[] { "{WINDIR}\\Prefetch" },
            WhySafe = "Data Windows records to speed up launching frequently used apps. It is rebuilt " +
                      "automatically as you use programs.",
            TypicalContents = "*.pf prefetch files and ReadyBoot traces.",
            Caution = "The first launch of some apps may be slightly slower until Prefetch rebuilds.",
        },
        new CleanupCategory
        {
            Id = "wer-machine",
            Name = "Windows Error Reporting (system)",
            Kind = CategoryKind.CrashDumps,
            Scope = CategoryScope.Machine,
            PathTemplates = new[] { "{PROGRAMDATA}\\Microsoft\\Windows\\WER" },
            WhySafe = "Queued and archived crash reports collected machine-wide. They are only useful for " +
                      "diagnosing crashes that already happened.",
            TypicalContents = "ReportArchive / ReportQueue crash report data.",
        },
        new CleanupCategory
        {
            Id = "nvidia-programdata",
            Name = "NVIDIA Installer & Driver Cache",
            Kind = CategoryKind.Installer,
            Redownloads = true,
            Scope = CategoryScope.Machine,
            PathTemplates = new[]
            {
                "{PROGRAMDATA}\\NVIDIA Corporation\\Downloader",
                "{PROGRAMDATA}\\NVIDIA Corporation\\NV_Cache",
            },
            WhySafe = "Driver packages the NVIDIA installer/GeForce Experience downloaded, plus a driver " +
                      "cache. They are re-downloaded or rebuilt when needed.",
            TypicalContents = "Downloaded driver installers and cached driver data.",
        },
        new CleanupCategory
        {
            Id = "visualstudio-installer-cache",
            Name = "Visual Studio Installer Cache",
            Kind = CategoryKind.Installer,
            Redownloads = true,
            Scope = CategoryScope.Machine,
            PathTemplates = new[] { "{PROGRAMDATA}\\Microsoft\\VisualStudio\\Packages" },
            WhySafe = "Installer packages the Visual Studio Installer downloaded. Visual Studio re-downloads " +
                      "any package it needs when you modify or repair an installation.",
            TypicalContents = "Cached VSIX/MSI installer payloads.",
            Caution = "Modifying or repairing Visual Studio will re-download these (needs internet and time).",
        },

        // ============================ User profile: Windows caches ============================
        new CleanupCategory
        {
            Id = "user-temp",
            Name = "User Temp Files",
            Kind = CategoryKind.TempFiles,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[] { "{LOCALAPPDATA}\\Temp", "{TEMP}" },
            WhySafe = "The per-user temporary folder (%TEMP%). Applications use it for short-lived working " +
                      "files and recreate anything they still need, so leftovers here are abandoned.",
            TypicalContents = "Installer leftovers, extracted archives, app scratch files, partial downloads.",
        },
        new CleanupCategory
        {
            Id = "crash-dumps",
            Name = "Application Crash Dumps",
            Kind = CategoryKind.CrashDumps,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[] { "{LOCALAPPDATA}\\CrashDumps" },
            WhySafe = "Memory snapshots written when an application crashes. They are only useful for " +
                      "debugging a crash that already happened.",
            TypicalContents = "*.dmp crash dump files.",
        },
        new CleanupCategory
        {
            Id = "wer-user",
            Name = "Windows Error Reporting (user)",
            Kind = CategoryKind.CrashDumps,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[]
            {
                "{LOCALAPPDATA}\\Microsoft\\Windows\\WER\\ReportArchive",
                "{LOCALAPPDATA}\\Microsoft\\Windows\\WER\\ReportQueue",
                "{LOCALAPPDATA}\\Microsoft\\Windows\\WER\\Temp",
            },
            WhySafe = "Your queued and archived crash reports. Only useful for diagnosing crashes that " +
                      "already happened.",
            TypicalContents = "Crash report folders and dump fragments.",
        },
        new CleanupCategory
        {
            Id = "inetcache",
            Name = "Windows Internet Cache (WinINet)",
            Kind = CategoryKind.Cache,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[] { "{LOCALAPPDATA}\\Microsoft\\Windows\\INetCache" },
            WhySafe = "Cached web content downloaded by Internet Explorer and Windows components. It is " +
                      "rebuilt automatically the next time the content is needed.",
            TypicalContents = "Cached web pages, images, scripts and other downloaded resources.",
        },
        new CleanupCategory
        {
            Id = "thumbnail-cache",
            Name = "Thumbnail & Icon Cache",
            Kind = CategoryKind.Cache,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[] { "{LOCALAPPDATA}\\Microsoft\\Windows\\Explorer" },
            WhySafe = "Thumbnails and icons that File Explorer generates for previews. They are regenerated " +
                      "on demand the next time you browse a folder.",
            TypicalContents = "thumbcache_*.db and iconcache_*.db database files.",
        },

        // ============================ User profile: GPU shader caches ============================
        new CleanupCategory
        {
            Id = "directx-shader-cache",
            Name = "DirectX Shader Cache",
            Kind = CategoryKind.Cache,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[] { "{LOCALAPPDATA}\\D3DSCache" },
            WhySafe = "Compiled DirectX shaders the GPU driver caches to speed up games and apps. They are " +
                      "recompiled automatically when missing.",
            TypicalContents = "Cached compiled shader programs.",
        },
        new CleanupCategory
        {
            Id = "nvidia-shader-cache",
            Name = "NVIDIA Shader Cache",
            Kind = CategoryKind.Cache,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[]
            {
                "{LOCALAPPDATA}\\NVIDIA\\DXCache",
                "{LOCALAPPDATA}\\NVIDIA\\GLCache",
                "{LOCALAPPDATA}\\NVIDIA Corporation\\NV_Cache",
            },
            WhySafe = "Compiled shader caches maintained by the NVIDIA driver. They are rebuilt on demand, " +
                      "so clearing them only costs a brief recompile.",
            TypicalContents = "Cached DirectX/OpenGL shader binaries.",
        },
        new CleanupCategory
        {
            Id = "amd-shader-cache",
            Name = "AMD Shader Cache",
            Kind = CategoryKind.Cache,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[]
            {
                "{LOCALAPPDATA}\\AMD\\DxCache",
                "{LOCALAPPDATA}\\AMD\\DxcCache",
                "{LOCALAPPDATA}\\AMD\\GLCache",
                "{LOCALAPPDATA}\\AMD\\VkCache",
            },
            WhySafe = "Compiled shader caches maintained by the AMD driver. They are rebuilt on demand, " +
                      "so clearing them only costs a brief recompile.",
            TypicalContents = "Cached DirectX/OpenGL/Vulkan shader binaries.",
        },
        new CleanupCategory
        {
            Id = "intel-shader-cache",
            Name = "Intel Shader Cache",
            Kind = CategoryKind.Cache,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[] { "{LOCALAPPDATA}\\Intel\\ShaderCache" },
            WhySafe = "Compiled shader cache maintained by the Intel graphics driver. Rebuilt on demand.",
            TypicalContents = "Cached compiled shader binaries.",
        },

        // ============================ Browsers ============================
        new CleanupCategory
        {
            Id = "chrome-cache",
            Name = "Google Chrome Cache",
            Kind = CategoryKind.Cache,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[]
            {
                "{LOCALAPPDATA}\\Google\\Chrome\\User Data\\*\\Cache",
                "{LOCALAPPDATA}\\Google\\Chrome\\User Data\\*\\Code Cache",
                "{LOCALAPPDATA}\\Google\\Chrome\\User Data\\*\\GPUCache",
                "{LOCALAPPDATA}\\Google\\Chrome\\User Data\\*\\GrShaderCache",
                "{LOCALAPPDATA}\\Google\\Chrome\\User Data\\*\\Service Worker\\CacheStorage",
            },
            WhySafe = "Copies of web resources Chrome keeps so pages load faster. They are re-downloaded as " +
                      "needed. Clearing the cache does not remove bookmarks, passwords or sign you out.",
            TypicalContents = "Cached HTTP responses, compiled scripts, GPU shader cache, service-worker data.",
        },
        new CleanupCategory
        {
            Id = "edge-cache",
            Name = "Microsoft Edge Cache",
            Kind = CategoryKind.Cache,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[]
            {
                "{LOCALAPPDATA}\\Microsoft\\Edge\\User Data\\*\\Cache",
                "{LOCALAPPDATA}\\Microsoft\\Edge\\User Data\\*\\Code Cache",
                "{LOCALAPPDATA}\\Microsoft\\Edge\\User Data\\*\\GPUCache",
                "{LOCALAPPDATA}\\Microsoft\\Edge\\User Data\\*\\GrShaderCache",
                "{LOCALAPPDATA}\\Microsoft\\Edge\\User Data\\*\\Service Worker\\CacheStorage",
            },
            WhySafe = "Copies of web resources Edge keeps so pages load faster. They are re-downloaded as " +
                      "needed. Clearing the cache does not remove favorites, passwords or sign you out.",
            TypicalContents = "Cached HTTP responses, compiled scripts, GPU shader cache, service-worker data.",
        },
        new CleanupCategory
        {
            Id = "brave-cache",
            Name = "Brave Browser Cache",
            Kind = CategoryKind.Cache,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[]
            {
                "{LOCALAPPDATA}\\BraveSoftware\\Brave-Browser\\User Data\\*\\Cache",
                "{LOCALAPPDATA}\\BraveSoftware\\Brave-Browser\\User Data\\*\\Code Cache",
                "{LOCALAPPDATA}\\BraveSoftware\\Brave-Browser\\User Data\\*\\GPUCache",
                "{LOCALAPPDATA}\\BraveSoftware\\Brave-Browser\\User Data\\*\\Service Worker\\CacheStorage",
            },
            WhySafe = "Copies of web resources Brave keeps so pages load faster. They are re-downloaded as " +
                      "needed and clearing them does not affect bookmarks or logins.",
            TypicalContents = "Cached HTTP responses, compiled scripts, GPU cache, service-worker data.",
        },
        new CleanupCategory
        {
            Id = "vivaldi-cache",
            Name = "Vivaldi Cache",
            Kind = CategoryKind.Cache,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[]
            {
                "{LOCALAPPDATA}\\Vivaldi\\User Data\\*\\Cache",
                "{LOCALAPPDATA}\\Vivaldi\\User Data\\*\\Code Cache",
                "{LOCALAPPDATA}\\Vivaldi\\User Data\\*\\GPUCache",
            },
            WhySafe = "Copies of web resources Vivaldi keeps so pages load faster. Re-downloaded as needed.",
            TypicalContents = "Cached HTTP responses, compiled scripts, GPU cache.",
        },
        new CleanupCategory
        {
            Id = "opera-cache",
            Name = "Opera Cache",
            Kind = CategoryKind.Cache,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[]
            {
                "{APPDATA}\\Opera Software\\Opera Stable\\Cache",
                "{APPDATA}\\Opera Software\\Opera GX Stable\\Cache",
                "{LOCALAPPDATA}\\Opera Software\\Opera Stable\\Cache",
                "{LOCALAPPDATA}\\Opera Software\\Opera GX Stable\\Cache",
            },
            WhySafe = "Copies of web resources Opera keeps so pages load faster. Re-downloaded as needed.",
            TypicalContents = "Cached HTTP responses and media.",
        },
        new CleanupCategory
        {
            Id = "firefox-cache",
            Name = "Mozilla Firefox Cache",
            Kind = CategoryKind.Cache,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[] { "{LOCALAPPDATA}\\Mozilla\\Firefox\\Profiles\\*\\cache2" },
            WhySafe = "Copies of web resources Firefox keeps so pages load faster. They are re-downloaded " +
                      "as needed and clearing them does not affect bookmarks or logins.",
            TypicalContents = "Cached HTTP responses stored in the cache2 directory.",
        },

        // ============================ IDEs & developer tools ============================
        new CleanupCategory
        {
            Id = "vscode-cache",
            Name = "Visual Studio Code Cache",
            Kind = CategoryKind.Cache,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[]
            {
                "{APPDATA}\\Code\\Cache",
                "{APPDATA}\\Code\\CachedData",
                "{APPDATA}\\Code\\Code Cache",
                "{APPDATA}\\Code\\GPUCache",
                "{APPDATA}\\Code\\logs",
                "{APPDATA}\\Code - Insiders\\Cache",
                "{APPDATA}\\Code - Insiders\\CachedData",
                "{APPDATA}\\Code - Insiders\\Code Cache",
                "{APPDATA}\\Code - Insiders\\GPUCache",
            },
            WhySafe = "Caches the VS Code editor (an Electron app) rebuilds on launch. Clearing them does " +
                      "not affect your settings, keybindings or installed extensions.",
            TypicalContents = "Compiled scripts, GPU cache, downloaded update payloads and logs.",
        },
        new CleanupCategory
        {
            Id = "jetbrains-cache",
            Name = "JetBrains IDE Caches (IntelliJ, Rider, PyCharm…)",
            Kind = CategoryKind.Cache,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[]
            {
                "{LOCALAPPDATA}\\JetBrains\\*\\caches",
                "{LOCALAPPDATA}\\JetBrains\\*\\log",
                "{LOCALAPPDATA}\\JetBrains\\*\\tmp",
            },
            WhySafe = "Per-version index caches, logs and temp files for JetBrains IDEs. The IDE rebuilds " +
                      "its indexes on the next launch; your projects and settings are untouched.",
            TypicalContents = "Project index caches, IDE logs, temporary files.",
            Caution = "The next launch of the affected IDE will re-index projects, which can take a while.",
        },
        new CleanupCategory
        {
            Id = "visualstudio-componentcache",
            Name = "Visual Studio Component Cache",
            Kind = CategoryKind.Cache,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[] { "{LOCALAPPDATA}\\Microsoft\\VisualStudio\\*\\ComponentModelCache" },
            WhySafe = "Visual Studio's MEF component cache. It is rebuilt automatically and is a common fix " +
                      "for VS extension glitches.",
            TypicalContents = "Cached extension/component composition data.",
        },

        // ============================ Package-manager caches ============================
        new CleanupCategory
        {
            Id = "nuget-http-cache",
            Name = ".NET NuGet HTTP Cache",
            Kind = CategoryKind.Cache,
            Redownloads = true,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[] { "{LOCALAPPDATA}\\NuGet\\v3-cache", "{LOCALAPPDATA}\\NuGet\\plugins-cache" },
            WhySafe = "Temporary copies of package metadata and downloads NuGet keeps while restoring. They " +
                      "are re-fetched automatically when needed.",
            TypicalContents = "Cached package metadata and download responses.",
        },
        new CleanupCategory
        {
            Id = "nuget-global-packages",
            Name = "NuGet Global Package Cache",
            Kind = CategoryKind.Cache,
            Redownloads = true,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[] { "{USERPROFILE}\\.nuget\\packages" },
            WhySafe = "Extracted copies of NuGet packages shared across all your .NET projects. They are " +
                      "restored automatically on the next build or restore.",
            TypicalContents = "Extracted package folders organized by package id and version.",
            Caution = "The next build of each project will re-download these packages (needs internet and time).",
        },
        new CleanupCategory
        {
            Id = "npm-cache",
            Name = "npm Cache",
            Kind = CategoryKind.Cache,
            Redownloads = true,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[] { "{LOCALAPPDATA}\\npm-cache", "{APPDATA}\\npm-cache" },
            WhySafe = "Cached npm package downloads. npm restores them from the registry on demand.",
            TypicalContents = "Content-addressable package tarballs and metadata.",
            Caution = "Cleared packages are re-downloaded on the next 'npm install'.",
        },
        new CleanupCategory
        {
            Id = "pnpm-store",
            Name = "pnpm Store",
            Kind = CategoryKind.Cache,
            Redownloads = true,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[] { "{LOCALAPPDATA}\\pnpm\\store", "{LOCALAPPDATA}\\pnpm-store" },
            WhySafe = "pnpm's content-addressable package store. Packages are re-downloaded on demand.",
            TypicalContents = "Hard-linked package content shared across projects.",
            Caution = "Cleared packages are re-downloaded on the next 'pnpm install'.",
        },
        new CleanupCategory
        {
            Id = "yarn-cache",
            Name = "Yarn Cache",
            Kind = CategoryKind.Cache,
            Redownloads = true,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[] { "{LOCALAPPDATA}\\Yarn\\Cache", "{LOCALAPPDATA}\\Yarn\\cache" },
            WhySafe = "Cached package downloads Yarn keeps to speed up installs. Re-downloaded as needed.",
            TypicalContents = "Content-addressable package archives.",
            Caution = "Cleared packages are re-downloaded on the next 'yarn install'.",
        },
        new CleanupCategory
        {
            Id = "pip-cache",
            Name = "Python pip Cache",
            Kind = CategoryKind.Cache,
            Redownloads = true,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[] { "{LOCALAPPDATA}\\pip\\Cache" },
            WhySafe = "Cached Python wheels and downloads pip keeps to speed up installs. Re-downloaded as needed.",
            TypicalContents = "Cached wheels and HTTP download responses.",
            Caution = "Cleared items are re-downloaded on the next 'pip install'.",
        },
        new CleanupCategory
        {
            Id = "gradle-cache",
            Name = "Gradle Cache",
            Kind = CategoryKind.Cache,
            Redownloads = true,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[] { "{USERPROFILE}\\.gradle\\caches" },
            WhySafe = "Downloaded dependencies and build cache Gradle keeps to speed up builds. Restored " +
                      "automatically on the next build.",
            TypicalContents = "Cached dependency jars and build outputs.",
            Caution = "The next Gradle build will re-download dependencies (needs internet and time).",
        },
        new CleanupCategory
        {
            Id = "maven-cache",
            Name = "Maven Local Repository",
            Kind = CategoryKind.Cache,
            Redownloads = true,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[] { "{USERPROFILE}\\.m2\\repository" },
            WhySafe = "Downloaded Maven artifacts shared across Java projects. Restored automatically on the " +
                      "next build.",
            TypicalContents = "Cached dependency jars organized by group/artifact/version.",
            Caution = "The next build will re-download dependencies (needs internet and time).",
        },
        new CleanupCategory
        {
            Id = "cargo-cache",
            Name = "Rust Cargo Cache",
            Kind = CategoryKind.Cache,
            Redownloads = true,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[] { "{USERPROFILE}\\.cargo\\registry\\cache", "{USERPROFILE}\\.cargo\\registry\\src" },
            WhySafe = "Downloaded crate archives and unpacked sources Cargo keeps. Re-downloaded on demand.",
            TypicalContents = "Cached crate downloads and extracted crate sources.",
            Caution = "Cleared crates are re-downloaded on the next 'cargo build'.",
        },
        new CleanupCategory
        {
            Id = "go-build-cache",
            Name = "Go Build Cache",
            Kind = CategoryKind.Cache,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[] { "{LOCALAPPDATA}\\go-build" },
            WhySafe = "Compiled build artifacts the Go toolchain caches to speed up rebuilds. Rebuilt on demand.",
            TypicalContents = "Cached compiled package objects.",
        },
        new CleanupCategory
        {
            Id = "composer-cache",
            Name = "PHP Composer Cache",
            Kind = CategoryKind.Cache,
            Redownloads = true,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[] { "{LOCALAPPDATA}\\Composer" },
            WhySafe = "Downloaded PHP packages Composer keeps to speed up installs. Re-downloaded on demand.",
            TypicalContents = "Cached package downloads and metadata.",
            Caution = "Cleared packages are re-downloaded on the next 'composer install'.",
        },

        // ============================ Communication & media apps ============================
        new CleanupCategory
        {
            Id = "discord-cache",
            Name = "Discord Cache",
            Kind = CategoryKind.Cache,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[]
            {
                "{APPDATA}\\discord\\Cache",
                "{APPDATA}\\discord\\Code Cache",
                "{APPDATA}\\discord\\GPUCache",
            },
            WhySafe = "Caches Discord (an Electron app) rebuilds on launch. Clearing them does not sign you " +
                      "out or remove your servers/messages.",
            TypicalContents = "Cached HTTP responses, compiled scripts, GPU cache.",
        },
        new CleanupCategory
        {
            Id = "slack-cache",
            Name = "Slack Cache",
            Kind = CategoryKind.Cache,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[]
            {
                "{APPDATA}\\Slack\\Cache",
                "{APPDATA}\\Slack\\Code Cache",
                "{APPDATA}\\Slack\\GPUCache",
                "{APPDATA}\\Slack\\Service Worker\\CacheStorage",
            },
            WhySafe = "Caches the Slack desktop app rebuilds on launch. Clearing them does not sign you out.",
            TypicalContents = "Cached HTTP responses, compiled scripts, GPU and service-worker caches.",
        },
        new CleanupCategory
        {
            Id = "teams-cache",
            Name = "Microsoft Teams Cache",
            Kind = CategoryKind.Cache,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[]
            {
                "{APPDATA}\\Microsoft\\Teams\\Cache",
                "{APPDATA}\\Microsoft\\Teams\\Code Cache",
                "{APPDATA}\\Microsoft\\Teams\\GPUCache",
                "{APPDATA}\\Microsoft\\Teams\\Service Worker\\CacheStorage",
            },
            WhySafe = "Caches the classic Teams desktop app rebuilds on launch. Clearing them does not sign " +
                      "you out or remove chats.",
            TypicalContents = "Cached HTTP responses, compiled scripts, GPU and service-worker caches.",
        },
        new CleanupCategory
        {
            Id = "spotify-cache",
            Name = "Spotify Cache",
            Kind = CategoryKind.Cache,
            Redownloads = true,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[] { "{LOCALAPPDATA}\\Spotify\\Storage", "{LOCALAPPDATA}\\Spotify\\Data" },
            WhySafe = "Streamed audio Spotify caches for faster playback. Re-downloaded as needed.",
            TypicalContents = "Cached audio segments and media data.",
            Caution = "Downloaded-for-offline tracks may need to be re-downloaded.",
        },
        new CleanupCategory
        {
            Id = "zoom-logs",
            Name = "Zoom Logs & Cache",
            Kind = CategoryKind.Logs,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[] { "{APPDATA}\\Zoom\\logs", "{APPDATA}\\Zoom\\data\\cache" },
            WhySafe = "Diagnostic logs and cache for the Zoom client. Only useful for troubleshooting.",
            TypicalContents = "Client logs and cached UI assets.",
        },
        new CleanupCategory
        {
            Id = "adobe-media-cache",
            Name = "Adobe Media Cache",
            Kind = CategoryKind.Cache,
            Scope = CategoryScope.UserProfile,
            PathTemplates = new[]
            {
                "{APPDATA}\\Adobe\\Common\\Media Cache Files",
                "{APPDATA}\\Adobe\\Common\\Media Cache",
            },
            WhySafe = "Conformed/indexed media Adobe apps (Premiere, After Effects) cache to speed up editing. " +
                      "Rebuilt automatically from your source media when needed.",
            TypicalContents = "Conformed audio and peak (indexed) media files.",
            Caution = "Open projects may take longer to load while the cache is rebuilt.",
        },
    };
}
