<#
.SYNOPSIS
    Renders the Tekla Lookup app-icon design to the two PNGs the TSEP needs.

.DESCRIPTION
    Reuses the same vector logo as Build-AppIcon.ps1 (flat steel I-beam +
    magnifying glass, beam slice under the lens lit blue) and rasterizes it via
    WPF — no external SVG tooling required — to:

        installer\TeklaLookup.png        256x256  Extensions Manager icon
        installer\Macro\Tekla Lookup.png  96x96   Applications & components thumbnail

    Re-run whenever the icon design changes (keep the design here in sync with
    Build-AppIcon.ps1). WPF wants an STA thread:
        pwsh -STA -ExecutionPolicy Bypass -File branding\Build-InstallerIcons.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

# --- Design (256-px space) — kept identical to Build-AppIcon.ps1 ----------------
$LensCenter = @(150, 116); $LensRadius = 56
$Ibeam = @(104,41, 196,41, 196,67, 165,67, 165,165, 196,165, 196,191,
           104,191, 104,165, 135,165, 135,67, 104,67)
$GrayFill = 'B6C2D1'

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

function New-BlueBrush {
    # Vertical brand gradient #3B82F6 -> #1D4FD7.
    $g = New-Object System.Windows.Media.LinearGradientBrush
    $g.StartPoint = Point 0 0; $g.EndPoint = Point 0 1
    $g.GradientStops.Add((New-Object System.Windows.Media.GradientStop ((Color '3B82F6'), 0.0)))
    $g.GradientStops.Add((New-Object System.Windows.Media.GradientStop ((Color '1D4FD7'), 1.0)))
    $g
}

function New-LogoDrawing {
    $root = New-Object System.Windows.Media.DrawingGroup

    # Gray I-beam.
    $root.Children.Add((New-Polygon $Ibeam (Brush $GrayFill)))

    # Same beam in blue, clipped to the lens circle (the lookup hit).
    $blueGroup = New-Object System.Windows.Media.DrawingGroup
    $blueGroup.ClipGeometry = New-Object System.Windows.Media.EllipseGeometry ((Point $LensCenter[0] $LensCenter[1]), $LensRadius, $LensRadius)
    $blueGroup.Children.Add((New-Polygon $Ibeam (New-BlueBrush)))
    $root.Children.Add($blueGroup)

    $ring = New-BlueBrush

    # Magnifier handle (under the ring).
    $handle = New-Object System.Windows.Media.GeometryDrawing
    $handle.Geometry = New-Object System.Windows.Media.LineGeometry ((Point 190 156), (Point 232 206))
    $hp = New-Object System.Windows.Media.Pen ($ring, 24); $hp.StartLineCap = 'Round'; $hp.EndLineCap = 'Round'
    $handle.Pen = $hp; $root.Children.Add($handle)

    # Lens: faint glass fill + thick gradient ring.
    $lens = New-Object System.Windows.Media.GeometryDrawing
    $lens.Geometry = New-Object System.Windows.Media.EllipseGeometry ((Point $LensCenter[0] $LensCenter[1]), $LensRadius, $LensRadius)
    $lens.Brush = Brush 'FFFFFF' 0.10
    $lens.Pen = New-Object System.Windows.Media.Pen ($ring, 16); $root.Children.Add($lens)

    return $root
}

function Write-Png([int]$size, [string]$outPath) {
    $vis = New-Object System.Windows.Media.DrawingVisual
    $ctx = $vis.RenderOpen()
    $ctx.PushTransform((New-Object System.Windows.Media.ScaleTransform (($size/256.0), ($size/256.0))))
    $ctx.DrawDrawing((New-LogoDrawing)); $ctx.Pop(); $ctx.Close()
    $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap ($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $rtb.Render($vis)
    $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($rtb))

    $resolved = [System.IO.Path]::GetFullPath($outPath)
    $dir = [System.IO.Path]::GetDirectoryName($resolved)
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $fs = [System.IO.File]::Create($resolved); $enc.Save($fs); $fs.Close()
    Write-Output ("Wrote {0} ({1}x{1}, {2:n0} bytes)" -f $resolved, $size, (Get-Item $resolved).Length)
}

$installer = Join-Path $PSScriptRoot '..\installer'
Write-Png 256 (Join-Path $installer 'TeklaLookup.png')
Write-Png  96 (Join-Path $installer 'Macro\Tekla Lookup.png')
