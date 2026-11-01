[CmdletBinding()]
param(
    [switch]$SkipSetup
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$csc = Join-Path $root "tools\roslyn\tools\csc.exe"
if (-not (Test-Path $csc)) {
    throw "Roslyn compiler was not found at $csc"
}

$framework = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
$wpf = Join-Path $framework "WPF"
if (-not (Test-Path $wpf)) {
    throw "WPF assemblies were not found at $wpf"
}

$references = @(
    (Join-Path $framework "mscorlib.dll"),
    (Join-Path $framework "System.dll"),
    (Join-Path $framework "System.Core.dll"),
    (Join-Path $framework "System.Data.dll"),
    (Join-Path $framework "System.Xml.dll"),
    (Join-Path $framework "System.Drawing.dll"),
    (Join-Path $framework "System.Web.dll"),
    (Join-Path $framework "System.Web.Extensions.dll"),
    (Join-Path $framework "System.Xaml.dll"),
    (Join-Path $wpf "WindowsBase.dll"),
    (Join-Path $wpf "PresentationCore.dll"),
    (Join-Path $wpf "PresentationFramework.dll"),
    (Join-Path $wpf "UIAutomationTypes.dll"),
    (Join-Path $wpf "UIAutomationClient.dll")
)
foreach ($reference in $references) {
    if (-not (Test-Path $reference)) {
        throw "Missing reference assembly: $reference"
    }
}
$referenceArgs = $references | ForEach-Object { "/r:$($_)" }

$build = Join-Path $root "build"
$dist = Join-Path $root "dist"
New-Item -ItemType Directory -Force -Path $build | Out-Null
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$baseArgs = @(
    "/nologo",
    "/target:winexe",
    "/platform:anycpu",
    "/langversion:latest",
    "/optimize+",
    "/utf8output",
    "/win32icon:assets\app.ico",
    "/win32manifest:assets\app.manifest",
    "/resource:assets\app.png,AutoBackup.Logo.png"
)

function Invoke-Compile {
    param(
        [string]$Name,
        [string]$Output,
        [string[]]$Sources,
        [string[]]$Resources = @()
    )
    $arguments = @()
    $arguments += $baseArgs
    $arguments += $referenceArgs
    $arguments += $Resources
    $arguments += "/out:$Output"
    foreach ($source in $Sources) {
        $arguments += $source
    }
    Write-Host "Building $Name..." -ForegroundColor Cyan
    & $csc @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Compilation failed for $Name (exit code $LASTEXITCODE)."
    }
    if (-not (Test-Path $Output)) {
        throw "Compiler did not produce $Output"
    }
}

$sharedSources = @(Get-ChildItem -Path "src\shared" -Filter *.cs -Recurse | ForEach-Object { $_.FullName })
$appSources = @()
$appSources += $sharedSources
$appSources += @(Get-ChildItem -Path "src\app" -Filter *.cs -Recurse | ForEach-Object { $_.FullName })

Invoke-Compile -Name "AutoBackup" -Output "build\AutoBackup.exe" -Sources $appSources

$toolSources = @()
$toolSources += $sharedSources
$toolSources += @(Get-ChildItem -Path "src\app\Services" -Filter *.cs | ForEach-Object { $_.FullName })
$toolSources += (Resolve-Path "src\app\Dialogs.cs").Path
$toolSources += (Resolve-Path "src\setup\InstallCommon.cs").Path

$uninstallerSources = @()
$uninstallerSources += $toolSources
$uninstallerSources += (Resolve-Path "src\setup\UninstallProgram.cs").Path
Invoke-Compile -Name "Uninstall" -Output "build\Uninstall.exe" -Sources $uninstallerSources

if (-not $SkipSetup) {
    $setupSources = @()
    $setupSources += $toolSources
    $setupSources += (Resolve-Path "src\setup\SetupProgram.cs").Path
    $setupResources = @(
        "/resource:build\AutoBackup.exe,AutoBackup.App.exe",
        "/resource:build\Uninstall.exe,AutoBackup.Uninstall.exe"
    )
    Invoke-Compile -Name "Setup" -Output "dist\AutoBackupSetup.exe" -Sources $setupSources -Resources $setupResources
}

Write-Host ""
Write-Host "Build complete." -ForegroundColor Green
$outputs = @("build\AutoBackup.exe", "build\Uninstall.exe")
if (-not $SkipSetup) { $outputs += "dist\AutoBackupSetup.exe" }
Get-Item $outputs | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
