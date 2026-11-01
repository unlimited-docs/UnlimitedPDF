<#
.SYNOPSIS
    Generates the project-authored demo images (PR-011) with the Windows imaging APIs (WPF).

.DESCRIPTION
    Draws simple shapes only (no fonts or third-party artwork):
      logo.png         160 x 160 px, transparent background, document icon on a blue circle
      keyboard.jpg      96 x 64 px, keyboard illustration
      dock.jpg          96 x 64 px, docking station illustration
      cmyk-sample.jpg  128 x 96 px, CMYK JPEG with cyan, magenta, yellow, and black quadrants

    Requires Windows PowerShell 5.1 (STA) on Windows. Run from any folder:
        powershell -NoProfile -ExecutionPolicy Bypass -File Generate-DemoAssets.ps1
#>
[CmdletBinding()]
param(
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
if (-not $OutputDirectory) { $OutputDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path }
Add-Type -AssemblyName PresentationCore, WindowsBase

function Get-Brush([string] $hex) {
    $brush = New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.ColorConverter]::ConvertFromString($hex))
    $brush.Freeze()
    return $brush
}

function Render([int] $w, [int] $h, [scriptblock] $draw) {
    $visual = New-Object System.Windows.Media.DrawingVisual
    $context = $visual.RenderOpen()
    & $draw $context
    $context.Close()
    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($w, $h, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    return $bitmap
}

function Save([System.Windows.Media.Imaging.BitmapEncoder] $encoder, [System.Windows.Media.Imaging.BitmapSource] $bitmap, [string] $name) {
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $path = Join-Path $OutputDirectory $name
    $stream = [System.IO.File]::Create($path)
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
    Write-Host "Wrote $name"
}

function Save-Png([System.Windows.Media.Imaging.BitmapSource] $bitmap, [string] $name) {
    # Straight (not premultiplied) alpha, as PNG stores it.
    $converted = New-Object System.Windows.Media.Imaging.FormatConvertedBitmap($bitmap, [System.Windows.Media.PixelFormats]::Bgra32, $null, 0)
    Save (New-Object System.Windows.Media.Imaging.PngBitmapEncoder) $converted $name
}

function Save-Jpeg([System.Windows.Media.Imaging.BitmapSource] $bitmap, [string] $name) {
    $converted = New-Object System.Windows.Media.Imaging.FormatConvertedBitmap($bitmap, [System.Windows.Media.PixelFormats]::Bgr24, $null, 0)
    $encoder = New-Object System.Windows.Media.Imaging.JpegBitmapEncoder
    $encoder.QualityLevel = 92
    Save $encoder $converted $name
}

function Rect([double] $x, [double] $y, [double] $w, [double] $h) { New-Object System.Windows.Rect($x, $y, $w, $h) }

# Logo: a white document with a folded corner and text lines, on a blue circle with an orange ring; the corners of
# the square image stay transparent.
$logo = Render 160 160 {
    param($dc)
    $dc.DrawEllipse((Get-Brush '#1E5AA8'), (New-Object System.Windows.Media.Pen((Get-Brush '#F28C28'), 8)), (New-Object System.Windows.Point(80, 80)), 72, 72)
    $page = New-Object System.Windows.Media.StreamGeometry
    $ctx = $page.Open()
    $ctx.BeginFigure((New-Object System.Windows.Point(52, 38)), $true, $true)
    $ctx.LineTo((New-Object System.Windows.Point(94, 38)), $true, $false)
    $ctx.LineTo((New-Object System.Windows.Point(110, 54)), $true, $false)
    $ctx.LineTo((New-Object System.Windows.Point(110, 122)), $true, $false)
    $ctx.LineTo((New-Object System.Windows.Point(52, 122)), $true, $false)
    $ctx.Close()
    $page.Freeze()
    $dc.DrawGeometry((Get-Brush '#FFFFFF'), $null, $page)
    $dc.DrawRectangle((Get-Brush '#C9D6EA'), $null, (Rect 94 38 16 16))
    foreach ($y in 64, 78, 92, 106) { $dc.DrawRectangle((Get-Brush '#1E5AA8'), $null, (Rect 60 $y 42 6)) }
}
Save-Png $logo 'logo.png'

# Keyboard: dark body with rows of light keys.
$keyboard = Render 96 64 {
    param($dc)
    $dc.DrawRectangle((Get-Brush '#F2F2F2'), $null, (Rect 0 0 96 64))
    $dc.DrawRoundedRectangle((Get-Brush '#2F3640'), $null, (Rect 6 16 84 34), 4, 4)
    for ($row = 0; $row -lt 3; $row++) {
        for ($key = 0; $key -lt 9; $key++) { $dc.DrawRectangle((Get-Brush '#DCDDE1'), $null, (Rect (10 + $key * 8.6) (20 + $row * 8) 6.5 6)) }
    }
    $dc.DrawRectangle((Get-Brush '#DCDDE1'), $null, (Rect 28 44 40 4))
}
Save-Jpeg $keyboard 'keyboard.jpg'

# Docking station: grey box with colored ports and a status light.
$dock = Render 96 64 {
    param($dc)
    $dc.DrawRectangle((Get-Brush '#F2F2F2'), $null, (Rect 0 0 96 64))
    $dc.DrawRoundedRectangle((Get-Brush '#57606F'), $null, (Rect 8 20 80 26), 5, 5)
    $ports = '#1E90FF', '#1E90FF', '#2F3640', '#2F3640', '#F28C28'
    for ($i = 0; $i -lt $ports.Count; $i++) { $dc.DrawRectangle((Get-Brush $ports[$i]), $null, (Rect (16 + $i * 12) 30 8 6)) }
    $dc.DrawEllipse((Get-Brush '#2ED573'), $null, (New-Object System.Windows.Point(80, 33)), 3, 3)
}
Save-Jpeg $dock 'dock.jpg'

# CMYK sample: ink amounts per quadrant (255 = full ink). The Windows encoder marks CMYK JPEGs with an Adobe segment.
$width = 128
$height = 96
$pixels = New-Object byte[] ($width * $height * 4)
for ($y = 0; $y -lt $height; $y++) {
    for ($x = 0; $x -lt $width; $x++) {
        $ink = if ($y -lt ($height / 2)) { if ($x -lt ($width / 2)) { 255, 0, 0, 0 } else { 0, 255, 0, 0 } } else { if ($x -lt ($width / 2)) { 0, 0, 255, 0 } else { 0, 0, 0, 255 } }
        $i = ($y * $width + $x) * 4
        for ($c = 0; $c -lt 4; $c++) { $pixels[$i + $c] = $ink[$c] }
    }
}
$cmyk = [System.Windows.Media.Imaging.BitmapSource]::Create($width, $height, 96, 96, [System.Windows.Media.PixelFormats]::Cmyk32, $null, $pixels, $width * 4)
$cmykEncoder = New-Object System.Windows.Media.Imaging.JpegBitmapEncoder
$cmykEncoder.QualityLevel = 92
Save $cmykEncoder $cmyk 'cmyk-sample.jpg'
