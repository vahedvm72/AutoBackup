namespace AutoBackup;

public static class AppInfo
{
    public const string Name = "Auto Backup";
    public const string Version = "1.3.1";
    public const string SettingsFileName = "AutoBackup.settings.json";
    public const string ArchivePrefix = "Backup_";
    public const string ExeName = "AutoBackup.exe";
    public const string UninstallerName = "Uninstall.exe";
    public const string UninstallRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\AutoBackup";
    public const string AppRegistryKey = @"Software\AutoBackup";
    public const string BootstrapFolderName = "AutoBackup";
}
