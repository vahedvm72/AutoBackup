using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AutoBackup.Services;
using AutoBackup.UI;
using AutoBackup.Views;
using Microsoft.Win32;

namespace AutoBackup;

public sealed class ShellWindow : ChromeWindow
{
    private AppConfig _config;
    private string _settingsPath;
    private string _expectedSettingsPath;
    private CancellationTokenSource _cts;
    private bool _busy;
    private string _homeNotice;
    private string _discoveredBackupFolder;

    private enum OperationKind
    {
        Backup,
        Restore,
        Selection
    }

    public ShellWindow(ConfigLoadResult load)
        : base(AppInfo.Name, 1000, 720, false, false, true, "Automatic folder backup")
    {
        var version = Ui.Badge("v" + AppInfo.Version, Ui.Dim, Ui.Hex("#1A1A1A"));
        version.Margin = new Thickness(0, 0, 8, 0);
        HeaderExtras.Children.Add(version);

        CloseGuard = delegate
        {
            if (!_busy)
            {
                return true;
            }
            DialogService.Alert(this, "A task is still running",
                "Auto Backup is working on your files. Cancel the task on the progress screen, then close the app.", false);
            return false;
        };

        if (load != null && load.HasConfig)
        {
            _config = load.Config;
            _settingsPath = load.SettingsPath;
            EnsureWinRar();
            ShowHome(false);
        }
        else if (load == null || load.Status == ConfigLoadStatus.NoBootstrap)
        {
            if (!TryLinkExistingSettings(null))
            {
                ShowWizard(_discoveredBackupFolder);
            }
        }
        else
        {
            _expectedSettingsPath = load.SettingsPath;
            if (!TryLinkExistingSettings(load.SettingsPath))
            {
                ShowRecovery(load);
            }
        }
    }

    private bool TryLinkExistingSettings(string unusablePath)
    {
        var found = ConfigStore.DiscoverExisting();
        if (!found.HasSettings)
        {
            if (found.HasBackups)
            {
                _discoveredBackupFolder = found.BackupFolder;
            }
            return false;
        }

        try
        {
            ConfigStore.SaveBootstrap(found.SettingsPath);
        }
        catch
        {
            return false;
        }

        _config = found.Config;
        _settingsPath = found.SettingsPath;
        EnsureWinRar();
        _homeNotice = string.IsNullOrEmpty(unusablePath)
            ? "Your existing settings were found on this device and linked automatically."
            : "Your settings file was found at a new location and linked automatically.";
        if (found.ArchiveCount > 0)
        {
            _homeNotice += " " + found.ArchiveCount
                + (found.ArchiveCount == 1 ? " backup is" : " backups are")
                + " ready to restore.";
        }
        ShowHome(false);
        return true;
    }

    private void ShowHome(bool animate = true)
    {
        if (_config == null)
        {
            ShowWizard();
            return;
        }
        SetTitle(AppInfo.Name, "Automatic folder backup");
        ShowBack(false);
        ShowSettings(true, OpenSettings);
        var notice = _homeNotice;
        _homeNotice = null;
        var view = new HomeView(_config, _settingsPath, AddBackupJob, EditBackupJob, DeleteBackupJob, RunSelection, ShowPrevious, OpenSettings, notice);
        ShowView(view, animate);
    }

    private void ShowPrevious()
    {
        if (_config == null)
        {
            ShowWizard();
            return;
        }
        SetTitle("Previous Backups", "Restore an archive on this device");
        ShowBack(true, delegate { ShowHome(); });
        ShowSettings(true, OpenSettings);
        var view = new PreviousView(_config, RestoreArchive, delegate { ShowHome(); });
        ShowView(view);
    }

    private void OpenSettings()
    {
        if (_config == null)
        {
            ShowWizard();
            return;
        }
        SetTitle("Settings", "Shared configuration");
        ShowBack(true, delegate { ShowHome(); });
        ShowSettings(false);
        var view = new SettingsView(_config, _settingsPath, ConfirmUninstall);
        view.Saved += delegate(AppConfig config, string path)
        {
            _config = config;
            _settingsPath = path;
            ShowHome();
        };
        view.SettingsMoved += delegate(string path)
        {
            _settingsPath = path;
        };
        ShowView(view);
    }

    private void ShowWizard(string initialBackupFolder = null)
    {
        SetTitle("Set up Auto Backup", "First-time configuration");
        ShowBack(false);
        ShowSettings(false);
        var view = new WizardView(initialBackupFolder);
        view.Completed += delegate(AppConfig config, string path)
        {
            _config = config;
            _settingsPath = path;
            EnsureWinRar();
            ShowHome();
        };
        ShowView(view);
    }

    private void ShowRecovery(ConfigLoadResult load)
    {
        SetTitle("Link shared settings", "Auto Backup needs your settings file");
        ShowBack(false);
        ShowSettings(false);
        var view = new RecoveryView(load, LocateSettingsFile, ChooseSettingsFolder, delegate { ShowWizard(); });
        ShowView(view);
    }

    private void LocateSettingsFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Locate " + AppInfo.SettingsFileName,
            Filter = "Auto Backup settings (" + AppInfo.SettingsFileName + ")|" + AppInfo.SettingsFileName + "|JSON files (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        try
        {
            if (!string.IsNullOrEmpty(_expectedSettingsPath))
            {
                var directory = Path.GetDirectoryName(_expectedSettingsPath);
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    dialog.InitialDirectory = directory;
                    dialog.FileName = Path.GetFileName(_expectedSettingsPath);
                }
            }
            if (dialog.ShowDialog(this) == true)
            {
                LinkSettings(dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            DialogService.Alert(this, "The settings file could not be opened", ex.Message, true);
        }
    }

    private void ChooseSettingsFolder()
    {
        try
        {
            var initial = ConfigStore.SuggestedSettingsFolder;
            if (!string.IsNullOrEmpty(_expectedSettingsPath))
            {
                var directory = Path.GetDirectoryName(_expectedSettingsPath);
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    initial = directory;
                }
            }
            var folder = FolderPicker.Pick(this, "Choose the folder that contains " + AppInfo.SettingsFileName, initial);
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }
            var path = Path.GetFullPath(ConfigStore.BuildSettingsPath(folder));
            if (!File.Exists(path))
            {
                DialogService.Alert(this, "No settings file found",
                    "Auto Backup did not find " + AppInfo.SettingsFileName + " in this folder:\n" + folder +
                    "\n\nChoose the correct folder, locate the file directly, or start setup to create new settings.", true);
                return;
            }
            LinkSettings(path);
        }
        catch (Exception ex)
        {
            DialogService.Alert(this, "The settings folder could not be used", ex.Message, true);
        }
    }

    private void LinkSettings(string settingsPath)
    {
        AppConfig config;
        string error;
        if (!ConfigStore.TryReadSettings(settingsPath, out config, out error))
        {
            DialogService.Alert(this, "The settings file could not be read",
                Path.GetFileName(settingsPath) + " could not be loaded:\n" + error, true);
            return;
        }
        try
        {
            ConfigStore.SaveBootstrap(settingsPath);
        }
        catch (Exception ex)
        {
            DialogService.Alert(this, "This device could not be linked", ex.Message, true);
            return;
        }
        _config = config;
        _settingsPath = settingsPath;
        EnsureWinRar();
        ShowHome();
    }

    private void EnsureWinRar()
    {
        if (_config == null || WinRarLocator.IsValid(_config.WinRarPath))
        {
            return;
        }
        var detected = WinRarLocator.Detect();
        if (!string.IsNullOrEmpty(detected))
        {
            _config.WinRarPath = detected;
        }
    }

    private void AddBackupJob()
    {
        if (_config == null)
        {
            ShowWizard();
            return;
        }
        var jobs = ArchiveService.GetJobs(_config);
        var initial = new BackupJob
        {
            Name = UniqueJobName(jobs, "Backup"),
            SourceFolder = string.Empty,
            BackupFolder = jobs.Count > 0 ? jobs[jobs.Count - 1].BackupFolder : AppConfig.CleanPath(_config.BackupFolder)
        };
        var result = DialogService.ShowJob(this, "New backup", initial, delegate(BackupJob candidate)
        {
            return JobNameExists(jobs, candidate.Name, -1)
                ? "A backup with this name already exists. Choose another name."
                : null;
        });
        if (result == null)
        {
            return;
        }
        jobs.Add(result);
        SaveJobs(jobs, "Backup added.");
    }

    private void EditBackupJob(BackupJob job)
    {
        if (_config == null || job == null)
        {
            return;
        }
        var jobs = ArchiveService.GetJobs(_config);
        var index = jobs.FindIndex(delegate(BackupJob item) { return SameJob(item, job); });
        if (index < 0)
        {
            ShowHome();
            return;
        }
        var result = DialogService.ShowJob(this, "Edit backup", jobs[index].Clone(), delegate(BackupJob candidate)
        {
            return JobNameExists(jobs, candidate.Name, index)
                ? "A backup with this name already exists. Choose another name."
                : null;
        });
        if (result == null)
        {
            return;
        }
        jobs[index] = result;
        SaveJobs(jobs, "Backup updated.");
    }

    private void DeleteBackupJob(BackupJob job)
    {
        if (_config == null || job == null)
        {
            return;
        }
        var jobs = ArchiveService.GetJobs(_config);
        var index = jobs.FindIndex(delegate(BackupJob item) { return SameJob(item, job); });
        if (index < 0)
        {
            return;
        }
        if (!DialogService.Confirm(this, "Delete this backup?",
            "\"" + jobs[index].Name + "\" will be removed from the list.\n\nExisting archives stay in their backup folder.",
            "Delete", true))
        {
            return;
        }
        jobs.RemoveAt(index);
        SaveJobs(jobs, "Backup deleted.");
    }

    private void SaveJobs(List<BackupJob> jobs, string notice)
    {
        _config.Jobs = jobs;
        _config.Normalize();
        try
        {
            ConfigStore.SaveSettings(_config, _settingsPath);
        }
        catch (Exception ex)
        {
            DialogService.Alert(this, "The change could not be saved", ex.Message, true);
            return;
        }
        _homeNotice = notice;
        ShowHome();
    }

    private static bool JobNameExists(List<BackupJob> jobs, string name, int exceptIndex)
    {
        for (var i = 0; i < jobs.Count; i++)
        {
            if (i == exceptIndex)
            {
                continue;
            }
            if (string.Equals(jobs[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool SameJob(BackupJob first, BackupJob second)
    {
        return string.Equals(first.Name, second.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(first.SourceFolder, second.SourceFolder, StringComparison.OrdinalIgnoreCase)
            && string.Equals(first.BackupFolder, second.BackupFolder, StringComparison.OrdinalIgnoreCase);
    }

    private static string UniqueJobName(List<BackupJob> jobs, string baseName)
    {
        var name = baseName + " " + (jobs.Count + 1);
        var suffix = 2;
        while (JobNameExists(jobs, name, -1))
        {
            name = baseName + " " + (jobs.Count + 1) + " (" + suffix.ToString() + ")";
            suffix++;
        }
        return name;
    }

    private async void RunSelection(HomeSelection selection)
    {
        if (_config == null || _busy || selection == null)
        {
            return;
        }
        var configured = ArchiveService.GetJobs(_config);
        var jobs = new List<BackupJob>();
        if (selection.BackupJobs != null)
        {
            foreach (var job in selection.BackupJobs)
            {
                var match = configured.FirstOrDefault(delegate(BackupJob item) { return SameJob(item, job); });
                if (match != null)
                {
                    jobs.Add(match);
                }
            }
        }
        var restores = selection.Restores ?? new List<HomeRestore>();
        if (jobs.Count == 0 && restores.Count == 0)
        {
            return;
        }

        if (jobs.Count > 0)
        {
            var validation = ArchiveService.ValidateForBackup(jobs, _config.WinRarPath);
            if (validation != null)
            {
                DialogService.Alert(this, "Backup cannot start", validation, true);
                return;
            }
        }

        foreach (var item in restores)
        {
            if (item == null || item.Archive == null || string.IsNullOrEmpty(item.TargetFolder) || !File.Exists(item.Archive.FullPath))
            {
                DialogService.Alert(this, "A selected archive cannot be restored",
                    "One of the selected archives or its destination is no longer available. Refresh the list and try again.", true);
                return;
            }
        }

        if (restores.Count > 0)
        {
            var lines = new StringBuilder();
            for (var i = 0; i < Math.Min(restores.Count, 6); i++)
            {
                lines.AppendLine("\u2022 " + restores[i].Archive.Name);
                lines.AppendLine("  \u2192 " + restores[i].TargetFolder);
            }
            if (restores.Count > 6)
            {
                lines.AppendLine("\u2022 and " + (restores.Count - 6) + " more...");
            }
            var message = restores.Count + (restores.Count == 1 ? " archive will" : " archives will")
                + " be extracted to its main folder:\n\n" + lines
                + "\nFiles with the same names will be overwritten.";
            if (jobs.Count > 0)
            {
                message += "\n\n" + jobs.Count
                    + (jobs.Count == 1 ? " selected backup will also run." : " selected backups will also run.");
            }
            if (!DialogService.Confirm(this, "Restore selected backups?", message, "Start", true))
            {
                return;
            }
        }

        _busy = true;
        _cts = new CancellationTokenSource();
        var cts = _cts;

        var hasBackups = jobs.Count > 0;
        var hasRestores = restores.Count > 0;
        var title = hasBackups && hasRestores
            ? "Working on your selection"
            : hasBackups ? "Creating your backups" : "Restoring your backups";
        string subtitle;
        if (hasBackups && hasRestores)
        {
            subtitle = "WinRAR is running " + jobs.Count + (jobs.Count == 1 ? " backup" : " backups")
                + " and restoring " + restores.Count + (restores.Count == 1 ? " archive." : " archives.");
        }
        else if (hasBackups)
        {
            subtitle = "WinRAR is running " + jobs.Count + (jobs.Count == 1 ? " backup" : " backups")
                + " and saving the archives to their backup folders.";
        }
        else
        {
            subtitle = "WinRAR is extracting " + restores.Count + (restores.Count == 1 ? " archive" : " archives")
                + " to the main folders.";
        }

        SetTitle(title, "WinRAR is working on the selected items");
        ShowBack(false);
        ShowSettings(false);
        var progress = new ProgressView(title, subtitle, hasBackups ? "\uE7B8" : "\uE896");
        progress.CancelRequested += delegate
        {
            try { cts.Cancel(); } catch { }
        };
        ShowView(progress);

        var started = DateTime.UtcNow;
        BackupResult backupResult = null;
        var restored = 0;
        var warning = false;
        var backupFolders = new List<string>();
        var restoreTargets = new List<string>();
        var seenFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var job in jobs)
        {
            if (!string.IsNullOrEmpty(job.BackupFolder) && seenFolders.Add(job.BackupFolder))
            {
                backupFolders.Add(job.BackupFolder);
            }
        }
        foreach (var item in restores)
        {
            restoreTargets.Add(item.TargetFolder);
        }

        try
        {
            var backupShare = hasBackups ? (hasRestores ? 60.0 : 100.0) : 0.0;
            var restoreShare = 100.0 - backupShare;

            if (hasBackups)
            {
                backupResult = await ArchiveService.CreateBackupAsync(jobs, _config.WinRarPath, delegate(double percent, string status)
                {
                    progress.SetProgress(percent * backupShare / 100.0, status);
                }, cts.Token);
                warning |= backupResult.Warning;
                ConfigStore.UpdateLastBackup(_config, _settingsPath);
            }

            for (var i = 0; i < restores.Count; i++)
            {
                cts.Token.ThrowIfCancellationRequested();
                var index = i;
                var result = await ArchiveService.RestoreAsync(_config, restores[index].Archive.FullPath, restores[index].TargetFolder, delegate(double percent, string status)
                {
                    var overall = backupShare + (restoreShare * ((index + percent / 100.0) / restores.Count));
                    progress.SetProgress(overall, status);
                }, cts.Token);
                warning |= result.Warning;
                restored++;
                ConfigStore.UpdateLastRestore(_config, _settingsPath);
            }

            ShowBatchResult(backupResult, restored, restoreTargets, backupFolders, warning, DateTime.UtcNow - started);
        }
        catch (OperationCanceledException)
        {
            ShowHome();
        }
        catch (AutoBackupException ex)
        {
            ShowFailure("The task could not finish", ex.Message, OperationKind.Selection);
        }
        catch (Exception ex)
        {
            ShowFailure("The task could not finish", ex.Message, OperationKind.Selection);
        }
        finally
        {
            _busy = false;
            _cts = null;
            try { cts.Dispose(); } catch { }
        }
    }

    private void ShowBatchResult(BackupResult backup, int restored, List<string> targets, List<string> backupFolders, bool warning, TimeSpan elapsed)
    {
        var created = backup != null ? backup.ArchivePaths.Count : 0;
        var createdSize = backup != null ? backup.SizeBytes : 0;
        var title = created > 0 && restored > 0
            ? "All done"
            : created > 0 ? "Backup complete" : "Restore complete";

        var parts = new List<string>();
        if (created > 0)
        {
            parts.Add(created + (created == 1 ? " backup was created" : " backups were created"));
        }
        if (restored > 0)
        {
            parts.Add(restored + (restored == 1 ? " archive was restored" : " archives were restored"));
        }
        var subtitle = string.Join(" and ", parts) + ".";

        string folder;
        string locationLabel;
        string location;
        if (created > 0)
        {
            if (backupFolders != null && backupFolders.Count > 0)
            {
                folder = backupFolders[0];
                locationLabel = backupFolders.Count > 1 ? "Backup folders" : "Backup folder";
                location = backupFolders.Count > 1
                    ? folder + "  +" + (backupFolders.Count - 1) + " more"
                    : folder;
            }
            else
            {
                folder = AppConfig.CleanPath(_config.BackupFolder);
                locationLabel = "Backup folder";
                location = folder;
            }
        }
        else
        {
            folder = targets.Count > 0 ? targets[0] : null;
            locationLabel = "Restored to";
            location = targets.Count == 1
                ? targets[0]
                : (targets.Count > 1 ? targets[0] + "  +" + (targets.Count - 1) + " more" : null);
        }

        SetTitle(title, "Your selection finished");
        ShowBack(false);
        ShowSettings(false);
        var content = new ResultContent
        {
            Success = true,
            Title = title,
            Subtitle = subtitle,
            LocationLabel = locationLabel,
            Location = location,
            Size = created > 0 ? Formats.Size(createdSize) : null,
            Duration = Formats.Duration(elapsed),
            Warning = warning
                ? "WinRAR finished with a warning for one or more items. Some files may have been locked or unreadable."
                : null,
            AutoCloseSeconds = 6
        };
        var view = new ResultView(content, delegate { Close(); }, folder != null ? new Action(delegate { OpenFolder(folder); }) : null, null);
        ShowView(view);
    }

    private void RestoreArchive(ArchiveInfo archive)
    {
        if (_config == null || _busy)
        {
            return;
        }
        var dialog = DialogService.ShowRestore(this, archive, _config);
        if (dialog == null || string.IsNullOrEmpty(dialog.Destination))
        {
            return;
        }
        RunRestore(archive, dialog.Destination);
    }

    private async void RunRestore(ArchiveInfo archive, string destination)
    {
        if (_busy)
        {
            return;
        }
        _busy = true;
        _cts = new CancellationTokenSource();
        var cts = _cts;

        SetTitle("Restoring backup", "Extracting your files");
        ShowBack(false);
        ShowSettings(false);

        var progress = new ProgressView("Restoring your backup",
            "WinRAR is extracting \"" + archive.Name + "\" into the destination folder.", "\uE896");
        progress.CancelRequested += delegate
        {
            try { cts.Cancel(); } catch { }
        };
        ShowView(progress);

        try
        {
            var result = await ArchiveService.RestoreAsync(_config, archive.FullPath, destination, progress.SetProgress, cts.Token);
            ConfigStore.UpdateLastRestore(_config, _settingsPath);
            ShowRestoreResult(result);
        }
        catch (OperationCanceledException)
        {
            ShowPrevious();
        }
        catch (AutoBackupException ex)
        {
            ShowFailure("Restore failed", ex.Message, OperationKind.Restore);
        }
        catch (Exception ex)
        {
            ShowFailure("Restore failed", ex.Message, OperationKind.Restore);
        }
        finally
        {
            _busy = false;
            _cts = null;
            try { cts.Dispose(); } catch { }
        }
    }

    private void ShowRestoreResult(RestoreResult result)
    {
        SetTitle("Restore complete", "Your files are back");
        ShowBack(false);
        ShowSettings(false);
        var content = new ResultContent
        {
            Success = true,
            Title = "Restore complete",
            Subtitle = "The archive was extracted successfully. Your files are ready in the destination folder.",
            LocationLabel = "Restored to",
            Location = result.TargetFolder,
            Duration = Formats.Duration(result.Duration),
            Warning = result.Warning
                ? "WinRAR finished with a warning. Check the destination folder if any files seem missing."
                : null,
            AutoCloseSeconds = 6
        };
        var destination = result.TargetFolder;
        var view = new ResultView(content, delegate { Close(); }, delegate { OpenFolder(destination); }, null);
        ShowView(view);
    }

    private void ShowFailure(string title, string message, OperationKind kind)
    {
        SetTitle(title, "Something needs your attention");
        ShowBack(false);
        ShowSettings(false);
        var content = new ResultContent
        {
            Success = false,
            Title = title,
            Subtitle = "The task could not be completed. The details below can help you fix the problem.",
            ErrorDetail = string.IsNullOrWhiteSpace(message) ? "An unknown error occurred." : message,
            AutoCloseSeconds = 0
        };
        Action retry = kind == OperationKind.Restore ? new Action(ShowPrevious) : new Action(delegate { ShowHome(); });
        Action close = kind == OperationKind.Restore ? new Action(ShowPrevious) : new Action(delegate { ShowHome(); });
        var view = new ResultView(content, close, null, retry);
        ShowView(view);
    }

    private void ConfirmUninstall()
    {
        var uninstaller = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppInfo.UninstallerName);
        if (!File.Exists(uninstaller))
        {
            DialogService.Alert(this, "Uninstall is not available",
                "This copy of Auto Backup was not installed with the setup program, so there is no uninstaller next to it.", false);
            return;
        }
        var confirmed = DialogService.Confirm(this, "Uninstall Auto Backup?",
            "The app will be removed from this PC.\n\nYour shared settings file and your backup archives are not deleted.",
            "Uninstall", true);
        if (!confirmed)
        {
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo { FileName = uninstaller, UseShellExecute = true });
            Close();
        }
        catch (Exception ex)
        {
            DialogService.Alert(this, "The uninstaller could not be started", ex.Message, true);
        }
    }

    private void OpenFolder(string folder)
    {
        try
        {
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = "\"" + folder + "\"", UseShellExecute = true });
        }
        catch (Exception ex)
        {
            DialogService.Alert(this, "The folder could not be opened", ex.Message, true);
        }
    }
}
