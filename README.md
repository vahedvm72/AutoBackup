# Auto Backup

A small Windows app that archives folders with WinRAR, keeps every backup job labelled and separated, and restores any archive back to its original folder.

The UI is written entirely in C# with WPF (no XAML), uses a monochrome theme, and ships with its own installer and uninstaller.

## Features

- **Named backup jobs** — each row has a custom name, a main folder, and its own backup folder.
- **Paired restore** — every job row lists its own archives; pick any archive of that job and restore it to the job's main folder.
- **Checklist workflow** — tick the backups to run and/or the archives to restore, then press **Start**. The app closes itself when the task finishes.
- **Multiple backup histories** — archives are named with the source-folder hash, so same-named folders from different locations never collide.
- **Shared settings** — one `AutoBackup.settings.json` file (for example on a synced drive) can be reused on every device; each PC only stores a small local pointer.
- **Automatic discovery** — on a new device the app searches common sync folders for an existing settings file and links it automatically.
- **WinRAR integration** — compresses with `Rar.exe`, reports live progress, supports cancel, and excludes the backup folder when it sits inside the source folder.
- **Installer** — per-user installation (no admin rights), Start menu/desktop shortcuts, and a clean uninstaller.

## Requirements

- Windows 10 or Windows 11 (x64)
- [WinRAR](https://www.win-rar.com/) installed (the app uses `Rar.exe`)
- To build: PowerShell 5.1+ and the .NET Framework 4.8 runtime assemblies

## Build

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Outputs:

| File | Purpose |
| --- | --- |
| `build\AutoBackup.exe` | Portable app |
| `build\Uninstall.exe` | Uninstaller |
| `dist\AutoBackupSetup.exe` | Self-contained installer (embeds the two files above) |

The Roslyn 4.2.0 compiler used for the build is included under `tools\roslyn`, so no SDK is required.

## How it works

1. On first launch, choose a folder for the shared settings file, the WinRAR path, and the first backup.
2. On the home screen, **New Backup** asks for:
   - a name you choose,
   - the main folder to compress,
   - the folder where the archive is stored.
3. Tick a backup and press **Start** to create a new archive. Tick an archive in the restore side to put it back into the job's main folder.
4. Use the arrows next to a restore archive to pick an older backup of that same job.

Settings are saved as JSON. A local pointer at `%LOCALAPPDATA%\AutoBackup\bootstrap.json` points to the shared settings file.

## Project layout

```
src/
  shared/        Window chrome, UI kit, app metadata, icon loader
  app/           App shell, services (config, WinRAR runner, archive logic), views, dialogs
  setup/         Installer, uninstaller, install helpers
assets/          App icon, logo, manifest
tools/           Roslyn compiler and helper scripts
build.ps1        Build script
```

## Third-party notices

- The build toolchain includes the Microsoft Roslyn compiler; see `tools\roslyn\ThirdPartyNotices.rtf`.
- WinRAR is required at runtime but is not bundled with this project.

## Notes

- The executables are not code-signed.
- The app stores no telemetry and makes no network requests of its own.
