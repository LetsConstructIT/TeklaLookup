<#
.SYNOPSIS
    Generates source/TeklaLookup.App/Resources/AppIcon.ico from the design in AppIcon.svg.

.DESCRIPTION
    Re-draws the icon shapes via System.Drawing at multiple resolutions (16/24/32/48/64/128/256)
    and packs them into a single Vista-style .ico (PNG-embedded for >=64, BMP-embedded for the
    rest). No external SVG rasterizer needed.

    Run from the repo root or from this directory:
        pwsh branding/Build-AppIcon.ps1
#>

[CmdletBinding()]
param(
    [string]$OutPath = (Join-Path $PSScriptRoot '..\source\TeklaLookup.App\Resources\AppIcon.ico')
)

Add-Type -AssemblyName System.Drawing

$BlueFrame  = [System.Drawing.Color]::FromArgb(0xFF, 0x00, 0x79, 0xC2)
$WhitePanel = [System.Drawing.Color]::FromArgb(0xFF, 0xFF, 0xFF, 0xFF)
$DarkGlyph  = [System.Drawing.Color]::FromArgb(0xFF, 0x1F, 0x1F, 0x1F)

function New-IconBitmap {
    param([int]$Size)

    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    # Scale all shape coords from the 256-px master design
    $s = $Size / 256.0
    function Sc([double]$v) { return [single]($v * $s) }

    # Blue rounded frame (256x256 master, corner radius 44)
    $frameRadius = Sc 44
    $framePath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $framePath.AddArc(0, 0, $frameRadius * 2, $frameRadius * 2, 180, 90)
    $framePath.AddArc((Sc 256) - $frameRadius * 2, 0, $frameRadius * 2, $frameRadius * 2, 270, 90)
    $framePath.AddArc((Sc 256) - $frameRadius * 2, (Sc 256) - $frameRadius * 2, $frameRadius * 2, $frameRadius * 2, 0, 90)
    $framePath.AddArc(0, (Sc 256) - $frameRadius * 2, $frameRadius * 2, $frameRadius * 2, 90, 90)
    $framePath.CloseFigure()
    $blueBrush = New-Object System.Drawing.SolidBrush($BlueFrame)
    $g.FillPath($blueBrush, $framePath)
    $blueBrush.Dispose()
    $framePath.Dispose()

    # White inner panel (220x220 inset by 18 from each side, corner radius 30)
    $panelRadius = Sc 30
    $panelX = Sc 18; $panelY = Sc 18
    $panelW = Sc 220; $panelH = Sc 220
    $panelPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $panelPath.AddArc($panelX, $panelY, $panelRadius * 2, $panelRadius * 2, 180, 90)
    $panelPath.AddArc($panelX + $panelW - $panelRadius * 2, $panelY, $panelRadius * 2, $panelRadius * 2, 270, 90)
    $panelPath.AddArc($panelX + $panelW - $panelRadius * 2, $panelY + $panelH - $panelRadius * 2, $panelRadius * 2, $panelRadius * 2, 0, 90)
    $panelPath.AddArc($panelX, $panelY + $panelH - $panelRadius * 2, $panelRadius * 2, $panelRadius * 2, 90, 90)
    $panelPath.CloseFigure()
    $whiteBrush = New-Object System.Drawing.SolidBrush($WhitePanel)
    $g.FillPath($whiteBrush, $panelPath)
    $whiteBrush.Dispose()
    $panelPath.Dispose()

    # Magnifying glass: lens ring + diagonal handle
    $glyphPen = New-Object System.Drawing.Pen($DarkGlyph, (Sc 18))
    $glyphPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $glyphPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    # Lens: center (108,108), radius 44 → bounding box at (64,64) size 88x88
    $g.DrawEllipse($glyphPen, (Sc 64), (Sc 64), (Sc 88), (Sc 88))
    $glyphPen.Dispose()

    $handlePen = New-Object System.Drawing.Pen($DarkGlyph, (Sc 22))
    $handlePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $handlePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($handlePen, (Sc 142), (Sc 142), (Sc 198), (Sc 198))
    $handlePen.Dispose()

    $g.Dispose()
    return $bmp
}

function ConvertTo-PngBytes {
    param([System.Drawing.Bitmap]$Bitmap)
    $ms = New-Object System.IO.MemoryStream
    $Bitmap.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    # Comma-wrap so PowerShell doesn't enumerate the byte[] into the pipeline.
    return ,$ms.ToArray()
}

# Build .ico file: header + N directory entries + N image payloads.
# Header (6 bytes): reserved=0, type=1 (icon), count=N
# Each directory entry (16 bytes): width, height, colors, reserved, planes, bitcount,
#   bytesInRes, imageOffset
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @()
foreach ($size in $sizes) {
    $bmp = New-IconBitmap -Size $size
    $png = ConvertTo-PngBytes -Bitmap $bmp
    $bmp.Dispose()
    $images += [pscustomobject]@{ Size = $size; Bytes = $png }
}

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)
$bw.Write([uint16]0)             # Reserved
$bw.Write([uint16]1)             # Type: icon
$bw.Write([uint16]$images.Count) # Image count

$headerSize = 6 + (16 * $images.Count)
$cursor = $headerSize
foreach ($img in $images) {
    $w = if ($img.Size -ge 256) { 0 } else { $img.Size }   # 0 means 256 in ICO format
    $h = $w
    $bw.Write([byte]$w)            # Width
    $bw.Write([byte]$h)            # Height
    $bw.Write([byte]0)             # Color count (0 for >=8bpp)
    $bw.Write([byte]0)             # Reserved
    $bw.Write([uint16]1)           # Color planes
    $bw.Write([uint16]32)          # Bits per pixel
    $bw.Write([uint32]$img.Bytes.Length) # Bytes in resource
    $bw.Write([uint32]$cursor)     # Offset to image data
    $cursor += $img.Bytes.Length
}
foreach ($img in $images) {
    $bw.Write($img.Bytes)
}
$bw.Flush()

$resolved = [System.IO.Path]::GetFullPath($OutPath)
$dir = [System.IO.Path]::GetDirectoryName($resolved)
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
[System.IO.File]::WriteAllBytes($resolved, $ms.ToArray())
$bw.Dispose()
$ms.Dispose()

Write-Output ("Wrote {0} ({1:n0} bytes, {2} sizes)" -f $resolved, (Get-Item $resolved).Length, $images.Count)
