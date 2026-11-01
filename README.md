# Auto Backup

## فارسی

برنامه سبک ویندوز برای بکاپ و ری‌استور پوشه‌ها با WinRAR.

**[دانلود نسخه قابل نصب](https://github.com/vahedvm72/AutoBackup/releases/latest)**

- هر بکاپ یک نام دلخواه، پوشه اصلی و پوشه بکاپ مستقل دارد
- بکاپ‌ها و ری‌استورها را تیک بزنید و **Start** را بزنید
- هر آرشیو به پوشه اصلی همان بکاپ برگردانده می‌شود
- یک فایل تنظیمات مشترک برای همه دستگاه‌ها
- بررسی و نصب خودکار آپدیت از گیت‌هاب، به‌همراه دکمه بررسی دستی در Settings

نیازمندی: ویندوز ۱۰ یا ۱۱ و WinRAR نصب‌شده.

## English

A lightweight Windows app that backs up folders with WinRAR and restores any archive back to its original folder.

**[Download the installer](https://github.com/vahedvm72/AutoBackup/releases/latest)**

- Each backup job has a custom name, a main folder, and its own backup folder
- Tick the backups and restores you want, then press **Start**
- Restores go back to the main folder of the same job
- One shared settings file works across all your devices
- Automatic update check/install from GitHub, plus a manual check in Settings

Requirements: Windows 10/11 and WinRAR.

### Build

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Outputs: `build\AutoBackup.exe`, `build\Uninstall.exe`, `dist\AutoBackupSetup.exe`.

The Roslyn compiler bundled in `tools\roslyn` is used by the build script; no SDK is required.
