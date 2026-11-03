<#
.SYNOPSIS
    Generates the project-authored JPEG test fixtures (PR-011) with the Windows imaging encoder (WPF/WIC).

.DESCRIPTION
    Every image is a 96 x 64 test pattern as displayed: four colored quadrants (top-left red, top-right green,
    bottom-left blue, bottom-right yellow) so a viewer that shows an image rotated, mirrored, or with wrong colors is
    easy to spot. Grayscale uses four distinct gray levels; CMYK uses cyan, magenta, yellow, and black.

    The EXIF orientation files store their pixels so the image appears upright only when the orientation is applied.

    Requires Windows PowerShell 5.1 or PowerShell 7 on Windows. Run from any folder:
        powershell -NoProfile -ExecutionPolicy Bypass -File Generate-JpegFixtures.ps1
#>
[CmdletBinding()]
param(
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
if (-not $OutputDirectory) { $OutputDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path }
Add-Type -AssemblyName PresentationCore, WindowsBase

$Width = 96
$Height = 64

# Returns the displayed pattern color at (x, y) as an RGB triple.
function Get-PatternColor([int] $x, [int] $y) {
    $left = $x -lt ($Width / 2)
    $top = $y -lt ($Height / 2)
    if ($top -and $left) { return 255, 0, 0 }
    if ($top) { return 0, 160, 0 }
    if ($left) { return 0, 0, 255 }
    return 255, 220, 0
}

function Save-Jpeg([System.Windows.Media.Imaging.BitmapSource] $Bitmap, [string] $Name, [Nullable[uint16]] $Orientation) {
    $encoder = New-Object System.Windows.Media.Imaging.JpegBitmapEncoder
    $encoder.QualityLevel = 90
    $metadata = $null
    if ($null -ne $Orientation) {
        $metadata = New-Object System.Windows.Media.Imaging.BitmapMetadata('jpg')
        $metadata.SetQuery('/app1/ifd/{ushort=274}', [uint16] $Orientation)
    }
    $frame = [System.Windows.Media.Imaging.BitmapFrame]::Create($Bitmap, $null, $metadata, $null)
    $encoder.Frames.Add($frame)
    $path = Join-Path $OutputDirectory $Name
    $stream = [System.IO.File]::Create($path)
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
    Write-Host "Wrote $Name"
}

function New-RgbBitmap([int] $w, [int] $h, [scriptblock] $colorAt, [double] $dpi = 96) {
    $stride = $w * 3
    $pixels = New-Object byte[] ($stride * $h)
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            $r, $g, $b = & $colorAt $x $y
            $i = $y * $stride + $x * 3
            $pixels[$i] = $b; $pixels[$i + 1] = $g; $pixels[$i + 2] = $r
        }
    }
    return [System.Windows.Media.Imaging.BitmapSource]::Create($w, $h, $dpi, $dpi, [System.Windows.Media.PixelFormats]::Bgr24, $null, $pixels, $stride)
}

# Baseline RGB at 96 dpi and at 300 dpi (the resolution is written to the JFIF header).
Save-Jpeg (New-RgbBitmap $Width $Height { param($x, $y) Get-PatternColor $x $y }) 'rgb-baseline.jpg' $null
Save-Jpeg (New-RgbBitmap $Width $Height { param($x, $y) Get-PatternColor $x $y } 300) 'rgb-300dpi.jpg' $null

# Baseline grayscale: four gray levels in the same quadrant layout.
$grayPixels = New-Object byte[] ($Width * $Height)
for ($y = 0; $y -lt $Height; $y++) {
    for ($x = 0; $x -lt $Width; $x++) {
        $level = if ($y -lt ($Height / 2)) { if ($x -lt ($Width / 2)) { 0 } else { 85 } } else { if ($x -lt ($Width / 2)) { 170 } else { 255 } }
        $grayPixels[$y * $Width + $x] = $level
    }
}
$gray = [System.Windows.Media.Imaging.BitmapSource]::Create($Width, $Height, 96, 96, [System.Windows.Media.PixelFormats]::Gray8, $null, $grayPixels, $Width)
Save-Jpeg $gray 'gray-baseline.jpg' $null

# Baseline CMYK: cyan, magenta, yellow, and black quadrants (values as ink amounts, 255 = full ink).
$cmykPixels = New-Object byte[] ($Width * $Height * 4)
for ($y = 0; $y -lt $Height; $y++) {
    for ($x = 0; $x -lt $Width; $x++) {
        $ink = if ($y -lt ($Height / 2)) { if ($x -lt ($Width / 2)) { 255, 0, 0, 0 } else { 0, 255, 0, 0 } } else { if ($x -lt ($Width / 2)) { 0, 0, 255, 0 } else { 0, 0, 0, 255 } }
        $i = ($y * $Width + $x) * 4
        for ($c = 0; $c -lt 4; $c++) { $cmykPixels[$i + $c] = $ink[$c] }
    }
}
$cmyk = [System.Windows.Media.Imaging.BitmapSource]::Create($Width, $Height, 96, 96, [System.Windows.Media.PixelFormats]::Cmyk32, $null, $cmykPixels, $Width * 4)
Save-Jpeg $cmyk 'cmyk-baseline.jpg' $null

# EXIF orientations 1 to 8. For each stored pixel (sx, sy), the displayed position follows the EXIF definition, so
# storing the pattern color of that displayed position makes the image upright once the orientation is applied.
for ($orientation = 1; $orientation -le 8; $orientation++) {
    $swap = $orientation -ge 5
    $storedWidth = if ($swap) { $Height } else { $Width }
    $storedHeight = if ($swap) { $Width } else { $Height }
    $o = $orientation
    $sw = $storedWidth
    $sh = $storedHeight
    $bitmap = New-RgbBitmap $storedWidth $storedHeight {
        param($sx, $sy)
        switch ($o) {
            1 { $dx = $sx; $dy = $sy }
            2 { $dx = $sw - 1 - $sx; $dy = $sy }
            3 { $dx = $sw - 1 - $sx; $dy = $sh - 1 - $sy }
            4 { $dx = $sx; $dy = $sh - 1 - $sy }
            5 { $dx = $sy; $dy = $sx }
            6 { $dx = $sh - 1 - $sy; $dy = $sx }
            7 { $dx = $sh - 1 - $sy; $dy = $sw - 1 - $sx }
            8 { $dx = $sy; $dy = $sw - 1 - $sx }
        }
        Get-PatternColor $dx $dy
    }.GetNewClosure()
    Save-Jpeg $bitmap ("exif-orientation-{0}.jpg" -f $orientation) ([uint16] $orientation)
}
