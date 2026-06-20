<#
.SYNOPSIS
    Generates source/TeklaLookup.App/Resources/AppIcon.ico from the design in AppIcon.svg.

.DESCRIPTION
    Draws the icon (flat steel I-beam + magnifying glass, with the beam slice under the
    lens lit blue) via WPF at multiple resolutions (16/24/32/48/64/128/256) and packs
    them into a single Vista-style PNG-embedded .ico. WPF is used (instead of
    System.Drawing) because the design needs a gradient stroke and a circular clip.

    Pass -EmitSvg to print the beam <polygon> elements for pasting into AppIcon.svg.

    No external SVG rasterizer needed, but WPF wants an STA thread:
        pwsh -STA -ExecutionPolicy Bypass -File branding/Build-AppIcon.ps1
        pwsh -STA -ExecutionPolicy Bypass -File branding/Build-AppIcon.ps1 -EmitSvg
#>

[CmdletBinding()]
param(
    [string]$OutPath = (Join-Path $PSScriptRoot '..\source\TeklaLookup.App\Resources\AppIcon.ico'),
    [switch]$EmitSvg
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

$sizes = 16, 24, 32, 48, 64, 128, 256

# --- Design (256-px space) ------------------------------------------------------
$LensCenter = @(150, 116); $LensRadius = 56

# Flat I-beam (IPE cross-section), centered on the lens and taller than it so the
# flanges read as a beam passing through the glass. Single closed outline; the slice
# inside the lens is redrawn in blue (the "active = blue" idea from SmartSelect).
$Ibeam = @(104,16, 196,16, 196,44, 166,44, 166,188, 196,188, 196,216,
           104,216, 104,188, 134,188, 134,44, 104,44)

$GrayFill = 'B6C2D1'

# --- WPF helpers ----------------------------------------------------------------
function Color([string]$hex) {
    [System.Windows.Media.Color]::FromRgb(
        [Convert]::ToInt32($hex.Substring(0,2),16),
        [Convert]::ToInt32($hex.Substring(2,2),16),
        [Convert]::ToInt32($hex.Substring(4,2),16))
}
function Brush([string]$hex, [double]$opacity = 1.0) {
    $b = New-Object System.Windows.Media.SolidColorBrush (Color $hex); $b.Opacity = $opacity; $b
}
function Point($x, $y) { New-Object System.Windows.Point ($x, $y) }

function New-Polygon([double[]]$pts, $brush) {
    $fig = New-Object System.Windows.Media.PathFigure
    $fig.StartPoint = Point $pts[0] $pts[1]; $fig.IsClosed = $true
    for ($i = 2; $i -lt $pts.Length; $i += 2) {
        $fig.Segments.Add((New-Object System.Windows.Media.LineSegment ((Point $pts[$i] $pts[$i+1]), $true)))
    }
    $geo = New-Object System.Windows.Media.PathGeometry; $geo.Figures.Add($fig)
    $d = New-Object System.Windows.Media.GeometryDrawing; $d.Geometry = $geo; $d.Brush = $brush; $d
}

function New-Gradient([string]$top, [string]$bottom) {
    $g = New-Object System.Windows.Media.LinearGradientBrush
    $g.StartPoint = Point 0 0; $g.EndPoint = Point 0 1
    $g.GradientStops.Add((New-Object System.Windows.Media.GradientStop ((Color $top), 0.0)))
    $g.GradientStops.Add((New-Object System.Windows.Media.GradientStop ((Color $bottom), 1.0)))
    $g
}
# Blue brand gradient for the magnifier (same as SmartSelect); amber->orange accent
# for the beam slice under the glass, so the "lookup hit" reads distinct from the lens.
function New-BlueBrush   { New-Gradient '3B82F6' '1D4FD7' }
function New-AccentBrush { New-Gradient 'FBBF24' 'F97316' }

function New-LogoDrawing {
    $root = New-Object System.Windows.Media.DrawingGroup

    # Gray I-beam.
    $root.Children.Add((New-Polygon $Ibeam (Brush $GrayFill)))

    # The beam slice under the glass, in the amber accent (the lookup hit).
    $hitGroup = New-Object System.Windows.Media.DrawingGroup
    $hitGroup.ClipGeometry = New-Object System.Windows.Media.EllipseGeometry ((Point $LensCenter[0] $LensCenter[1]), $LensRadius, $LensRadius)
    $hitGroup.Children.Add((New-Polygon $Ibeam (New-AccentBrush)))
    $root.Children.Add($hitGroup)

    $ring = New-BlueBrush

    # Magnifier handle. Starts just outside the lens ring (its round cap tucks under
    # the ring) so it doesn't poke into the glass.
    $handle = New-Object System.Windows.Media.GeometryDrawing
    $handle.Geometry = New-Object System.Windows.Media.LineGeometry ((Point 193 163), (Point 232 206))
    $hp = New-Object System.Windows.Media.Pen ($ring, 24); $hp.StartLineCap = 'Round'; $hp.EndLineCap = 'Round'
    $handle.Pen = $hp; $root.Children.Add($handle)

    # Lens: faint glass fill + thick gradient ring.
    $lens = New-Object System.Windows.Media.GeometryDrawing
    $lens.Geometry = New-Object System.Windows.Media.EllipseGeometry ((Point $LensCenter[0] $LensCenter[1]), $LensRadius, $LensRadius)
    $lens.Brush = Brush 'FFFFFF' 0.10
    $lens.Pen = New-Object System.Windows.Media.Pen ($ring, 16); $root.Children.Add($lens)

    return $root
}

# --- SVG emit (for keeping AppIcon.svg in sync) ---------------------------------
if ($EmitSvg) {
    $pairs = for ($i = 0; $i -lt $Ibeam.Length; $i += 2) { "$($Ibeam[$i]),$($Ibeam[$i+1])" }
    $pts = $pairs -join ' '
    Write-Output "  <polygon points=`"$pts`" fill=`"#$GrayFill`"/>            <!-- gray beam -->"
    Write-Output "  <polygon points=`"$pts`" fill=`"url(#hit)`" clip-path=`"url(#lens)`"/>  <!-- accent slice -->"
    return
}

# --- Render + pack --------------------------------------------------------------
function Convert-ToPng([int]$size) {
    $vis = New-Object System.Windows.Media.DrawingVisual
    $ctx = $vis.RenderOpen()
    $ctx.PushTransform((New-Object System.Windows.Media.ScaleTransform (($size/256.0), ($size/256.0))))
    $ctx.DrawDrawing((New-LogoDrawing)); $ctx.Pop(); $ctx.Close()
    $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap ($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $rtb.Render($vis)
    $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($rtb))
    $ms = New-Object System.IO.MemoryStream; $enc.Save($ms); return ,$ms.ToArray()
}

$pngs = @{}
foreach ($s in $sizes) { $pngs[$s] = Convert-ToPng $s }

$resolved = [System.IO.Path]::GetFullPath($OutPath)
$dir = [System.IO.Path]::GetDirectoryName($resolved)
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

$fs = [System.IO.File]::Create($resolved)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + (16 * $sizes.Count)
foreach ($s in $sizes) {
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$pngs[$s].Length); $bw.Write([uint32]$offset)
    $offset += $pngs[$s].Length
}
foreach ($s in $sizes) { $bw.Write($pngs[$s]) }
$bw.Flush(); $bw.Close(); $fs.Close()

Write-Output ("Wrote {0} ({1:n0} bytes, {2} sizes)" -f $resolved, (Get-Item $resolved).Length, $sizes.Count)
