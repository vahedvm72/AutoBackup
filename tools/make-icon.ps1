param(
    [string]$Output = (Join-Path (Split-Path -Parent $PSScriptRoot) "assets\app.ico")
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function New-RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Draw-Icon([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $scale = $size / 256.0
    $g.ScaleTransform($scale, $scale)

    # Rounded gradient tile
    $tile = New-RoundedPath 16 16 224 224 54
    $rect = New-Object System.Drawing.RectangleF 0, 0, 256, 256
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 91, 140, 255)), ([System.Drawing.Color]::FromArgb(255, 139, 92, 246)), 55.0
    $g.FillPath($brush, $tile)
    $brush.Dispose()

    # Soft top highlight
    $glossRect = New-Object System.Drawing.RectangleF 16, 16, 224, 120
    $gloss = New-Object System.Drawing.Drawing2D.LinearGradientBrush $glossRect, ([System.Drawing.Color]::FromArgb(45, 255, 255, 255)), ([System.Drawing.Color]::FromArgb(0, 255, 255, 255)), 90.0
    $clipPath = New-RoundedPath 16 16 224 224 54
    $g.SetClip($clipPath)
    $g.FillRectangle($gloss, $glossRect)
    $g.ResetClip()
    $gloss.Dispose()

    # Folder back tab
    $back = New-RoundedPath 62 84 132 92 16
    $backBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(150, 255, 255, 255))
    $g.FillPath($backBrush, $back)
    $backBrush.Dispose()

    # Folder front
    $front = New-RoundedPath 56 104 144 104 18
    $frontBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(250, 255, 255, 255))
    $g.FillPath($frontBrush, $front)
    $frontBrush.Dispose()

    # Up arrow (backup)
    $arrowRect = New-Object System.Drawing.RectangleF 100, 124, 56, 68
    $arrowBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $arrowRect, ([System.Drawing.Color]::FromArgb(255, 76, 125, 255)), ([System.Drawing.Color]::FromArgb(255, 124, 92, 255)), 90.0
    $head = New-Object System.Drawing.Drawing2D.GraphicsPath
    $head.AddPolygon(@(
        (New-Object System.Drawing.PointF 128, 126),
        (New-Object System.Drawing.PointF 102, 158),
        (New-Object System.Drawing.PointF 154, 158)
    ))
    $g.FillPath($arrowBrush, $head)
    $shaft = New-RoundedPath 118 152 20 38 5
    $g.FillPath($arrowBrush, $shaft)
    $arrowBrush.Dispose()
    $head.Dispose()

    $g.Dispose()
    return $bmp
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
$frames = @()
foreach ($s in $sizes) {
    $bmp = Draw-Icon $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames += ,@($s, $ms.ToArray())
    if ($s -eq 256) {
        $pngPath = [System.IO.Path]::ChangeExtension($Output, ".png")
        [System.IO.File]::WriteAllBytes($pngPath, $ms.ToArray())
    }
    $bmp.Dispose()
    $ms.Dispose()
}

$dir = Split-Path -Parent $Output
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
$fs = [System.IO.File]::Create($Output)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$frames.Count)
$offset = 6 + (16 * $frames.Count)
foreach ($frame in $frames) {
    $size = [int]$frame[0]
    $data = [byte[]]$frame[1]
    $bw.Write([byte]($(if ($size -ge 256) { 0 } else { $size })))
    $bw.Write([byte]($(if ($size -ge 256) { 0 } else { $size })))
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]$data.Length)
    $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($frame in $frames) {
    $bw.Write([byte[]]$frame[1])
}
$bw.Flush()
$bw.Close()
$fs.Close()
Write-Output "Created $Output"
