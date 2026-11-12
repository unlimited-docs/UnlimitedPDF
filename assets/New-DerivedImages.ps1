<#
.SYNOPSIS
    Makes every size-optimized copy of the UnlimitedPDF artwork from the originals in this folder.

.DESCRIPTION
    Writes the demo app's runtime images, the readme banner, the GitHub social preview, and the NuGet package icon,
    overwriting earlier copies, then prints a Markdown table of what it wrote. The originals are never changed
    (PR-011, PR-013). Run it from Windows PowerShell 5.1, which provides System.Drawing:

        powershell -NoProfile -ExecutionPolicy Bypass -File assets/New-DerivedImages.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$source = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class ArtworkCopies
{
    // Pixels with less alpha than this count as background when trimming and cleaning.
    private const int AlphaThreshold = 32;

    private static Bitmap Load(string path)
    {
        using (var source = new Bitmap(path))
        {
            var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.DrawImage(source, 0, 0, source.Width, source.Height);
            }
            return bitmap;
        }
    }

    private static byte[] ReadPixels(Bitmap bitmap)
    {
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var bytes = new byte[data.Stride * bitmap.Height];
        Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
        bitmap.UnlockBits(data);
        return bytes;
    }

    private static void WritePixels(Bitmap bitmap, byte[] bytes)
    {
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
        bitmap.UnlockBits(data);
    }

    // Clears specks: pixels not 8-connected to a visible area of at least minArea pixels, and faint pixels far from one.
    private static void RemoveSpecks(byte[] px, int w, int h, int minArea)
    {
        var label = new int[w * h];
        var stack = new int[w * h];
        var keep = new List<bool> { false };
        int next = 1;
        for (int start = 0; start < w * h; start++)
        {
            if (label[start] != 0 || px[start * 4 + 3] < AlphaThreshold) continue;
            int sp = 0, area = 0;
            stack[sp++] = start;
            label[start] = next;
            while (sp > 0)
            {
                int p = stack[--sp];
                area++;
                int x = p % w, y = p / w;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int q = ny * w + nx;
                        if (label[q] != 0 || px[q * 4 + 3] < AlphaThreshold) continue;
                        label[q] = next;
                        stack[sp++] = q;
                    }
            }
            keep.Add(area >= minArea);
            next++;
        }

        for (int p = 0; p < w * h; p++)
        {
            bool clear;
            if (label[p] != 0)
            {
                clear = !keep[label[p]];
            }
            else
            {
                clear = true;
                int x = p % w, y = p / w;
                for (int dy = -2; dy <= 2 && clear; dy++)
                    for (int dx = -2; dx <= 2 && clear; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int q = label[ny * w + nx];
                        if (q != 0 && keep[q]) clear = false;
                    }
            }
            if (clear)
            {
                px[p * 4] = 0; px[p * 4 + 1] = 0; px[p * 4 + 2] = 0; px[p * 4 + 3] = 0;
            }
        }
    }

    private static Rectangle VisibleBounds(byte[] px, int w, int h, double padFraction)
    {
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[(y * w + x) * 4 + 3] >= AlphaThreshold)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
        int cw = maxX - minX + 1, ch = maxY - minY + 1;
        int pad = (int)Math.Round(Math.Max(cw, ch) * padFraction);
        int x0 = Math.Max(0, minX - pad), y0 = Math.Max(0, minY - pad);
        int x1 = Math.Min(w - 1, maxX + pad), y1 = Math.Min(h - 1, maxY + pad);
        return new Rectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    private static Bitmap Resample(Bitmap source, Rectangle crop, int width, int height, Rectangle target, bool transparent)
    {
        var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(result))
        using (var attributes = new ImageAttributes())
        {
            g.Clear(transparent ? Color.Transparent : Color.White);
            g.CompositingMode = CompositingMode.SourceCopy;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            g.DrawImage(source, target, crop.X, crop.Y, crop.Width, crop.Height, GraphicsUnit.Pixel, attributes);
        }
        return result;
    }

    /// <summary>Trims transparent margins and fits the result inside maxWidth x maxHeight, as a PNG.</summary>
    public static string Trimmed(string input, string output, int maxWidth, int maxHeight, double padFraction, int specksMinArea)
    {
        using (var full = Load(input))
        {
            var px = ReadPixels(full);
            if (specksMinArea > 0)
            {
                RemoveSpecks(px, full.Width, full.Height, specksMinArea);
                WritePixels(full, px);
            }
            var crop = VisibleBounds(px, full.Width, full.Height, padFraction);
            double scale = Math.Min(1.0, Math.Min((double)maxWidth / crop.Width, (double)maxHeight / crop.Height));
            int w = Math.Max(1, (int)Math.Round(crop.Width * scale)), h = Math.Max(1, (int)Math.Round(crop.Height * scale));
            using (var copy = Resample(full, crop, w, h, new Rectangle(0, 0, w, h), true))
            {
                copy.Save(output, ImageFormat.Png);
            }
            return w + " x " + h;
        }
    }

    /// <summary>Trims transparent margins and centers the result on a transparent size x size square, as a PNG.</summary>
    public static string Square(string input, string output, int size)
    {
        using (var full = Load(input))
        {
            var crop = VisibleBounds(ReadPixels(full), full.Width, full.Height, 0);
            double scale = Math.Min((double)size / crop.Width, (double)size / crop.Height);
            int w = (int)Math.Round(crop.Width * scale), h = (int)Math.Round(crop.Height * scale);
            var target = new Rectangle((size - w) / 2, (size - h) / 2, w, h);
            using (var copy = Resample(full, crop, size, size, target, true))
            {
                copy.Save(output, ImageFormat.Png);
            }
            return size + " x " + size;
        }
    }

    /// <summary>Scales an opaque image to width x height, cropping the excess equally from both sides (cover).</summary>
    private static Bitmap Cover(Bitmap full, int width, int height)
    {
        double scale = Math.Max((double)width / full.Width, (double)height / full.Height);
        int cw = (int)Math.Round(width / scale), ch = (int)Math.Round(height / scale);
        var crop = new Rectangle((full.Width - cw) / 2, (full.Height - ch) / 2, cw, ch);
        return Resample(full, crop, width, height, new Rectangle(0, 0, width, height), false);
    }

    /// <summary>Writes an opaque copy as JPEG at the highest quality from 90 down that fits maxBytes.</summary>
    public static string CoverJpeg(string input, string output, int width, int height, long maxBytes)
    {
        using (var full = Load(input))
        using (var copy = Cover(full, width, height))
        {
            return SaveJpeg(copy, output, maxBytes);
        }
    }

    /// <summary>Scales an opaque image to the given width, keeping its proportions, as JPEG within maxBytes.</summary>
    public static string WidthJpeg(string input, string output, int width, long maxBytes)
    {
        using (var full = Load(input))
        {
            int height = (int)Math.Round((double)full.Height * width / full.Width);
            var whole = new Rectangle(0, 0, full.Width, full.Height);
            using (var copy = Resample(full, whole, width, height, new Rectangle(0, 0, width, height), false))
            {
                return SaveJpeg(copy, output, maxBytes);
            }
        }
    }

    private static string SaveJpeg(Bitmap copy, string output, long maxBytes)
    {
        var codec = Array.Find(ImageCodecInfo.GetImageEncoders(), c => c.FormatID == ImageFormat.Jpeg.Guid);
        for (long quality = 90; quality >= 40; quality -= 5)
        {
            using (var parameters = new EncoderParameters(1))
            using (var stream = new MemoryStream())
            {
                parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                copy.Save(stream, codec, parameters);
                if (stream.Length <= maxBytes || quality == 40)
                {
                    File.WriteAllBytes(output, stream.ToArray());
                    return copy.Width + " x " + copy.Height + ", JPEG quality " + quality;
                }
            }
        }
        throw new InvalidOperationException("unreachable");
    }
}
'@

Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing

$assets = $PSScriptRoot
$repo = Split-Path -Parent $assets
$images = Join-Path $repo 'UnlimitedPDF.Demos.BlazorWasm\wwwroot\images'
$derived = Join-Path $assets 'derived'
New-Item -ItemType Directory -Force -Path $derived | Out-Null

# Each entry: output (relative to the repository), original (relative to assets/), purpose, and how to make it.
$copies = @(
    @{ Out = 'UnlimitedPDF.Demos.BlazorWasm\wwwroot\images\unlimitedpdf-logo.png'; From = 'branding\unlimitedpdf-logo.png'; Use = 'App top bar wordmark'; Make = { param($i, $o) [ArtworkCopies]::Trimmed($i, $o, 440, 64, 0.0, 0) } },
    @{ Out = 'UnlimitedPDF.Demos.BlazorWasm\wwwroot\images\unlimitedpdf-icon.png'; From = 'branding\unlimitedpdf-icon.png'; Use = 'App top bar logo on phones'; Make = { param($i, $o) [ArtworkCopies]::Trimmed($i, $o, 64, 64, 0.0, 0) } },
    @{ Out = 'UnlimitedPDF.Demos.BlazorWasm\wwwroot\images\favicon.png'; From = 'branding\favicon.png'; Use = 'App browser tab icon'; Make = { param($i, $o) [ArtworkCopies]::Trimmed($i, $o, 64, 64, 0.0, 0) } },
    @{ Out = 'UnlimitedPDF.Demos.BlazorWasm\wwwroot\images\invoice-logo.png'; From = 'originals\invoice-logo.png'; Use = 'Invoice demo'; Make = { param($i, $o) [ArtworkCopies]::Trimmed($i, $o, 480, 120, 0.01, 0) } },
    @{ Out = 'UnlimitedPDF.Demos.BlazorWasm\wwwroot\images\medical-report-logo.png'; From = 'originals\medical-report-logo.png'; Use = 'Medical report demo'; Make = { param($i, $o) [ArtworkCopies]::Trimmed($i, $o, 480, 120, 0.01, 0) } },
    @{ Out = 'UnlimitedPDF.Demos.BlazorWasm\wwwroot\images\receipt-logo.png'; From = 'originals\receipt-logo.png'; Use = 'Receipt demo'; Make = { param($i, $o) [ArtworkCopies]::Trimmed($i, $o, 360, 100, 0.01, 0) } },
    @{ Out = 'UnlimitedPDF.Demos.BlazorWasm\wwwroot\images\purchase-order-logo.png'; From = 'originals\purchase-order-logo.png'; Use = 'Purchase order demo (specks cleared)'; Make = { param($i, $o) [ArtworkCopies]::Trimmed($i, $o, 480, 120, 0.01, 400) } },
    @{ Out = 'UnlimitedPDF.Demos.BlazorWasm\wwwroot\images\quotation-logo.png'; From = 'originals\quotation-logo.png'; Use = 'Quotation demo (specks cleared)'; Make = { param($i, $o) [ArtworkCopies]::Trimmed($i, $o, 480, 120, 0.01, 400) } },
    @{ Out = 'UnlimitedPDF.Demos.BlazorWasm\wwwroot\images\account-statement-logo.png'; From = 'originals\account-statement-logo.png'; Use = 'Account statement demo (specks cleared)'; Make = { param($i, $o) [ArtworkCopies]::Trimmed($i, $o, 480, 120, 0.01, 400) } },
    @{ Out = 'UnlimitedPDF.Demos.BlazorWasm\wwwroot\images\payslip-logo.png'; From = 'originals\payslip-logo.png'; Use = 'Payslip demo (specks cleared)'; Make = { param($i, $o) [ArtworkCopies]::Trimmed($i, $o, 480, 120, 0.01, 400) } },
    @{ Out = 'UnlimitedPDF.Demos.BlazorWasm\wwwroot\images\delivery-note-logo.png'; From = 'originals\delivery-note-logo.png'; Use = 'Delivery note demo (specks cleared)'; Make = { param($i, $o) [ArtworkCopies]::Trimmed($i, $o, 480, 120, 0.01, 400) } },
    @{ Out = 'UnlimitedPDF.Demos.BlazorWasm\wwwroot\images\certificate-seal.png'; From = 'originals\certificate-seal.png'; Use = 'Certificate demo (specks cleared)'; Make = { param($i, $o) [ArtworkCopies]::Trimmed($i, $o, 360, 360, 0.01, 400) } },
    @{ Out = 'UnlimitedPDF.Demos.BlazorWasm\wwwroot\images\certificate-signature.png'; From = 'originals\certificate-signature.png'; Use = 'Certificate demo (fictional signature)'; Make = { param($i, $o) [ArtworkCopies]::Trimmed($i, $o, 480, 140, 0.01, 0) } },
    @{ Out = 'UnlimitedPDF.Demos.BlazorWasm\wwwroot\images\unlimitedpdf-demos-hero-1200.jpg'; From = 'banners\unlimitedpdf-demos-hero.png'; Use = 'App home showcase (wide screens)'; Make = { param($i, $o) [ArtworkCopies]::CoverJpeg($i, $o, 1200, 675, 150000) } },
    @{ Out = 'UnlimitedPDF.Demos.BlazorWasm\wwwroot\images\unlimitedpdf-demos-hero-800.jpg'; From = 'banners\unlimitedpdf-demos-hero.png'; Use = 'App home showcase (phones)'; Make = { param($i, $o) [ArtworkCopies]::CoverJpeg($i, $o, 800, 450, 80000) } },
    @{ Out = 'assets\derived\unlimitedpdf-github-banner.jpg'; From = 'banners\unlimitedpdf-github-banner.png'; Use = 'README.md and nuget/README.md banner'; Make = { param($i, $o) [ArtworkCopies]::CoverJpeg($i, $o, 1280, 640, 250000) } },
    @{ Out = 'assets\derived\unlimitedpdf-social-preview.jpg'; From = 'banners\unlimitedpdf-social-card.png'; Use = 'GitHub repository social preview (uploaded in Settings)'; Make = { param($i, $o) [ArtworkCopies]::CoverJpeg($i, $o, 1280, 640, 400000) } },
    @{ Out = 'nuget\icon.png'; From = 'nuget\unlimitedpdf-package-icon.png'; Use = 'NuGet package icon'; Make = { param($i, $o) [ArtworkCopies]::Square($i, $o, 128) } }
)

# The home page gallery: each document's first page as its demo generates it, small for its tile and large for quick
# look (26111101 AC-07). The originals are captures; see ARTWORK.md.
New-Item -ItemType Directory -Force -Path (Join-Path $images 'gallery') | Out-Null
$gallery = 'invoice', 'quotation', 'purchase-order', 'delivery-note', 'account-statement', 'payslip', 'medical-report', 'receipt', 'certificate', 'employee-directory'
$copies += foreach ($name in $gallery) {
    @{ Out = "UnlimitedPDF.Demos.BlazorWasm\wwwroot\images\gallery\$name-320.jpg"; From = "originals\gallery\$name.png"; Use = 'App home gallery tile'; Make = { param($i, $o) [ArtworkCopies]::WidthJpeg($i, $o, 320, 40000) } }
    @{ Out = "UnlimitedPDF.Demos.BlazorWasm\wwwroot\images\gallery\$name-960.jpg"; From = "originals\gallery\$name.png"; Use = 'App home quick look'; Make = { param($i, $o) [ArtworkCopies]::WidthJpeg($i, $o, 960, 150000) } }
}

# The readme screenshots: what the README.md and nuget/README.md examples produce, at each capture's own width
# (QF-26111208). The originals are captures; see ARTWORK.md.
New-Item -ItemType Directory -Force -Path (Join-Path $derived 'readme') | Out-Null
$readme = [ordered]@{ 'sample' = 1172; 'invoice' = 1172; 'images' = 832; 'tables-across-pages' = 1224; 'headers-footers' = 1336; 'merge' = 1448; 'split' = 1672; 'html-to-pdf' = 832 }
$copies += foreach ($name in $readme.Keys) {
    $make = [scriptblock]::Create("param(`$i, `$o) [ArtworkCopies]::WidthJpeg(`$i, `$o, $($readme[$name]), 250000)")
    @{ Out = "assets\derived\readme\$name.jpg"; From = "originals\readme\$name.png"; Use = 'README.md and nuget/README.md screenshot'; Make = $make }
}

$rows = foreach ($copy in $copies) {
    $input = Join-Path $assets $copy.From
    $output = Join-Path $repo $copy.Out
    $size = & $copy.Make $input $output
    $bytes = (Get-Item -LiteralPath $output).Length
    [string]::Format([Globalization.CultureInfo]::InvariantCulture, '| `{0}` | `{1}` | {2} | {3:N0} | {4} |',
        ($copy.Out -replace '\\', '/'), ($copy.From -replace '\\', '/'), $size, $bytes, $copy.Use)
}

'| Copy (from the repository root) | Original (in `assets/`) | Size | Bytes | Used by |'
'|---|---|---|---|---|'
$rows
