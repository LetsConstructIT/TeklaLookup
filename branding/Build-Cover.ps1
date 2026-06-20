<#
.SYNOPSIS
    Renders a 4:3 cover image for store / Tekla Warehouse uploads.

.DESCRIPTION
    Logo on a white rounded card over a brand-blue gradient, with the "Tekla Lookup"
    wordmark below. Re-uses the same vector logo as Build-AppIcon.ps1 (flat I-beam +
    magnifier, amber lookup-hit) drawn via WPF, so no external tooling is needed.
    Keep New-LogoDrawing here in sync with Build-AppIcon.ps1 if the icon changes.

    WPF needs an STA thread:
        pwsh -STA -ExecutionPolicy Bypass -File branding/Build-Cover.ps1 -Width 600
#>
[CmdletBinding()]
param(
    [int]$Width = 600   # output width in px; height is derived to keep 4:3
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

$outPng = Join-Path $PSScriptRoot 'teklalookup-cover-4x3.png'

# The composition is authored in a fixed 1600x1200 design space, then scaled to the
# requested output size so text stays crisp at the target resolution.
$DW = 1600
$DH = 1200
$W  = $Width
$H  = [int][math]::Round($Width * 3.0 / 4.0)
$scale = $W / $DW

function Color([int]$r,[int]$g,[int]$b) { [System.Windows.Media.Color]::FromRgb($r,$g,$b) }
function Hex([string]$h) { Color ([Convert]::ToInt32($h.Substring(0,2),16)) ([Convert]::ToInt32($h.Substring(2,2),16)) ([Convert]::ToInt32($h.Substring(4,2),16)) }
function Rect($x,$y,$w,$h) { New-Object System.Windows.Rect ($x,$y,$w,$h) }
function Point($x,$y) { New-Object System.Windows.Point ($x,$y) }
function Gradient([string]$top,[string]$bottom) {
    $g = New-Object System.Windows.Media.LinearGradientBrush
    $g.StartPoint = Point 0 0; $g.EndPoint = Point 0 1
    $g.GradientStops.Add((New-Object System.Windows.Media.GradientStop ((Hex $top), 0.0)))
    $g.GradientStops.Add((New-Object System.Windows.Media.GradientStop ((Hex $bottom), 1.0)))
    $g
}

# --- the logo, identical geometry to Build-AppIcon.ps1 (256x256 design space) ---
function New-Polygon([double[]]$pts, $brush) {
    $fig = New-Object System.Windows.Media.PathFigure
    $fig.StartPoint = Point $pts[0] $pts[1]; $fig.IsClosed = $true
    for ($i = 2; $i -lt $pts.Length; $i += 2) {
        $fig.Segments.Add((New-Object System.Windows.Media.LineSegment ((Point $pts[$i] $pts[$i+1]), $true)))
    }
    $geo = New-Object System.Windows.Media.PathGeometry; $geo.Figures.Add($fig)
    $d = New-Object System.Windows.Media.GeometryDrawing; $d.Geometry = $geo; $d.Brush = $brush; $d
}

# Flat I-beam outline (256x256 design space); shared so section 3 can center on it.
$Ibeam = @(104,16, 196,16, 196,44, 166,44, 166,188, 196,188, 196,216,
           104,216, 104,188, 134,188, 134,44, 104,44)

function New-LogoDrawing {
    $LensCenter = @(150, 116); $LensRadius = 56

    $root = New-Object System.Windows.Media.DrawingGroup

    # Gray I-beam.
    $gray = New-Object System.Windows.Media.SolidColorBrush (Hex 'B6C2D1')
    $root.Children.Add((New-Polygon $Ibeam $gray))

    # Beam slice under the glass, amber accent (the lookup hit).
    $hit = New-Object System.Windows.Media.DrawingGroup
    $hit.ClipGeometry = New-Object System.Windows.Media.EllipseGeometry ((Point $LensCenter[0] $LensCenter[1]), $LensRadius, $LensRadius)
    $hit.Children.Add((New-Polygon $Ibeam (Gradient 'FBBF24' 'F97316')))
    $root.Children.Add($hit)

    # Magnifier handle. Starts just outside the lens ring (its round cap tucks under
    # the ring) so it doesn't poke into the glass.
    $handle = New-Object System.Windows.Media.GeometryDrawing
    $handle.Geometry = New-Object System.Windows.Media.LineGeometry ((Point 193 163), (Point 232 206))
    $hp = New-Object System.Windows.Media.Pen ((Gradient '3B82F6' '1D4FD7'), 24); $hp.StartLineCap = 'Round'; $hp.EndLineCap = 'Round'
    $handle.Pen = $hp; $root.Children.Add($handle)

    # Lens: faint glass fill + thick gradient ring.
    $lens = New-Object System.Windows.Media.GeometryDrawing
    $lens.Geometry = New-Object System.Windows.Media.EllipseGeometry ((Point $LensCenter[0] $LensCenter[1]), $LensRadius, $LensRadius)
    $glass = New-Object System.Windows.Media.SolidColorBrush (Color 0xFF 0xFF 0xFF); $glass.Opacity = 0.10
    $lens.Brush = $glass
    $lens.Pen = New-Object System.Windows.Media.Pen ((Gradient '3B82F6' '1D4FD7'), 16)
    $root.Children.Add($lens)

    return $root
}

$vis = New-Object System.Windows.Media.DrawingVisual
$ctx = $vis.RenderOpen()

# scale the whole 1600x1200 design down to the requested output size
$ctx.PushTransform((New-Object System.Windows.Media.ScaleTransform ($scale, $scale)))

# --- 1. brand-blue gradient background ----------------------------------------
$ctx.DrawRectangle((Gradient '3B82F6' '1D4FD7'), $null, (Rect 0 0 $DW $DH))

# --- 2. white rounded card to seat the logo -----------------------------------
$cardSize = 560
$cardX = ($DW - $cardSize) / 2
$cardY = 200
$cardBrush = New-Object System.Windows.Media.SolidColorBrush (Color 0xFF 0xFF 0xFF)
# subtle flat shadow for depth
$shadow = New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.Color]::FromArgb(40,0,0,0))
$ctx.DrawRoundedRectangle($shadow, $null, (Rect ($cardX) ($cardY + 12) $cardSize $cardSize), 48, 48)
$ctx.DrawRoundedRectangle($cardBrush, $null, (Rect $cardX $cardY $cardSize $cardSize), 48, 48)

# --- 3. logo centered on the card ---------------------------------------------
# Center on the I-BEAM's bounds, not the whole artwork: the magnifier handle sticks
# out to the lower-right, so centering the full drawing would shove the beam left.
# (The lens is concentric with the beam center, so it ends up centered too.)
$logo = New-LogoDrawing
$xs = @(); $ys = @()
for ($i = 0; $i -lt $Ibeam.Length; $i += 2) { $xs += $Ibeam[$i]; $ys += $Ibeam[$i+1] }
$bx0 = ($xs | Measure-Object -Minimum).Minimum; $bx1 = ($xs | Measure-Object -Maximum).Maximum
$by0 = ($ys | Measure-Object -Minimum).Minimum; $by1 = ($ys | Measure-Object -Maximum).Maximum
$beamCx = ($bx0 + $bx1) / 2; $beamCy = ($by0 + $by1) / 2
$logoScale = 390.0 / ($by1 - $by0)     # scale by I-beam height
$cardCenterX = $cardX + $cardSize / 2
$cardCenterY = $cardY + $cardSize / 2
$ctx.PushTransform((New-Object System.Windows.Media.TranslateTransform (($cardCenterX - $beamCx * $logoScale), ($cardCenterY - $beamCy * $logoScale))))
$ctx.PushTransform((New-Object System.Windows.Media.ScaleTransform ($logoScale, $logoScale)))
$ctx.DrawDrawing($logo)
$ctx.Pop()
$ctx.Pop()

# --- 4. "Tekla Lookup" wordmark, white, centered below the card ----------------
$typeface = New-Object System.Windows.Media.Typeface (
    (New-Object System.Windows.Media.FontFamily ("Segoe UI")),
    [System.Windows.FontStyles]::Normal,
    [System.Windows.FontWeights]::SemiBold,
    [System.Windows.FontStretches]::Normal)
$ft = New-Object System.Windows.Media.FormattedText (
    "Tekla Lookup",
    [System.Globalization.CultureInfo]::InvariantCulture,
    [System.Windows.FlowDirection]::LeftToRight,
    $typeface,
    132.0,
    [System.Windows.Media.Brushes]::White,
    1.0)
$textX = ($DW - $ft.Width) / 2
$textY = $cardY + $cardSize + 70
$ctx.DrawText($ft, (New-Object System.Windows.Point ($textX, $textY)))

$ctx.Pop()   # scale transform
$ctx.Close()

# --- render & save ------------------------------------------------------------
$rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap ($W, $H, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
$rtb.Render($vis)
$enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
$enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($rtb))
$fs = [System.IO.File]::Create($outPng)
$enc.Save($fs)
$fs.Close()

Write-Output "Wrote $outPng ($W x $H, 4:3)"
