<#
.SYNOPSIS
    Builds the Tekla Lookup TSEP installer for Tekla Structures.

.DESCRIPTION
    1. Publishes TeklaLookup.App -- the standalone WPF .exe (TeklaLookup.exe) --
       (Release, net48/x64) and its dependency closure into installer\BuildDrop.
       The launcher macro (installer\Macro) is packaged straight from source by
       BatchBuilder.
    2. Prunes assemblies that must NOT ship (the Tekla Open API DLLs are
       runtime-excluded already, but we belt-and-braces strip them plus any
       *.xml doc and test artifacts). The generic Release TeklaLookup.exe.config
       is kept (binding redirects etc.).
    3. Runs Tekla's BatchBuilder against installer\Manifest.xml to produce
       dist\TeklaLookup.<version>.tsep.

    MUST be built in Release. Tekla resolves the Open API runtime assemblies
    itself when the app is launched inside its environment. In Release the app
    is compiled against the 2021 Open API floor (Directory.Build.props,
    TeklaOpenApiVersion; members newer than 2021 are invoked by reflection at
    runtime), so one binary is forward-compatible across every later Tekla version
    and the
    exe.config carries no machine-specific Tekla codeBase. A Debug build's
    exe.config is patched (TSAppConfigPatcherTask) to point at THIS machine's
    Tekla install for F5 debugging -- packaging that would hard-code a local path
    and break on every other machine.

    Install the result via Tekla: Applications & components -> gear ->
    Manage extensions -> Install extension -> pick the .tsep. "Tekla Lookup"
    then appears in the catalog (via the launcher macro).

.PARAMETER TeklaVersion
    Tekla version whose BatchBuilder is used (default 2025.0). This only selects
    the packaging tool. Per Manifest.xml the package is built against 2021.0
    (matching the Release compile floor) and advertises support over
    2021.0..2099.1.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File build-tsep.ps1
#>
[CmdletBinding()]
param(
    [string]$TeklaVersion = "2025.0",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = 'Stop'

# A TSEP must be a Release build -- a Debug exe.config is patched to point at this
# machine's Tekla install and would break everywhere else. Tekla resolves the API
# assemblies at runtime for a Release build.
if ($Configuration -ne 'Release') {
    throw "Refusing to package a '$Configuration' build. TSEP must be Release " +
          "(a Debug exe.config hard-codes this machine's Tekla path)."
}

$root      = $PSScriptRoot
$installer = Join-Path $root 'installer'
$buildDrop = Join-Path $installer 'BuildDrop'
$manifest  = Join-Path $installer 'Manifest.xml'
$dist      = Join-Path $root 'dist'
$appCsproj = Join-Path $root 'source\TeklaLookup.App\TeklaLookup.App.csproj'

# --- locate BatchBuilder (fall back to the highest installed version) ----------
$batchBuilder = "C:\TeklaStructures\$TeklaVersion\bin\TeklaExtensionPackage.BatchBuilder.exe"
if (-not (Test-Path $batchBuilder)) {
    $candidate = Get-ChildItem 'C:\TeklaStructures' -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName 'bin\TeklaExtensionPackage.BatchBuilder.exe' } |
        Where-Object { Test-Path $_ } |
        Select-Object -First 1
    if (-not $candidate) {
        throw "BatchBuilder not found. Looked for $batchBuilder and scanned C:\TeklaStructures\*\bin."
    }
    Write-Host "Tekla $TeklaVersion not found; using $candidate" -ForegroundColor Yellow
    $batchBuilder = $candidate
}

# --- 1. publish the standalone app + dependency closure ------------------------
Write-Host "==> Publishing TeklaLookup.App ($Configuration) -> BuildDrop" -ForegroundColor Cyan
if (Test-Path $buildDrop) { Remove-Item $buildDrop -Recurse -Force }
dotnet publish $appCsproj -c $Configuration -o $buildDrop --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)." }

# --- 2. prune what must not ship -----------------------------------------------
# Tekla Open API assemblies come from the user's install (compile-only refs);
# never ship them. Also drop XML docs and any stray test artifacts.
# NB: the generic Release TeklaLookup.exe.config is KEPT (binding redirects).
# It is NOT machine-patched in Release -- Tekla resolves the API assemblies at
# runtime. (A Debug exe.config WOULD be path-patched; see the Release guard.)
$prune = @('Tekla.Structures*.dll', 'Tekla.Application.Library*.dll', '*.xml',
           '*.Tests.dll', 'nunit*.dll', 'FluentAssertions*.dll')
foreach ($pattern in $prune) {
    Get-ChildItem $buildDrop -Filter $pattern -ErrorAction SilentlyContinue |
        ForEach-Object { Write-Host "    pruning $($_.Name)"; Remove-Item $_.FullName -Force }
}

$shipped = Get-ChildItem $buildDrop -File | Select-Object -ExpandProperty Name
Write-Host "==> BuildDrop contents:" -ForegroundColor Cyan
$shipped | ForEach-Object { Write-Host "    $_" }
foreach ($required in 'TeklaLookup.exe', 'TeklaLookup.exe.config') {
    if ($shipped -notcontains $required) {
        throw "$required missing from BuildDrop -- aborting."
    }
}

# --- 3. build the .tsep --------------------------------------------------------
if (-not (Test-Path $dist)) { New-Item -ItemType Directory -Path $dist | Out-Null }
$product = ([xml](Get-Content $manifest)).TEP.Product
$version = $product.Version
# TSEP 3.0 naming scheme (Tekla 2025+): the builder ignores any filename in -o
# and always writes "<ProductId>.<Version>.tsep" into the -o *directory*.
$output  = Join-Path $dist ("{0}.{1}.tsep" -f $product.Id, $version)
if (Test-Path $output) { Remove-Item $output -Force }

Write-Host "==> Running BatchBuilder" -ForegroundColor Cyan
# Tekla 2025+ BatchBuilder is verb-based: `package -i <manifest> -o <dir>`.
# (Older docs show a bare `-i/-o` form; that fails with "No verb selected".)
& $batchBuilder package -i $manifest -o $dist
if ($LASTEXITCODE -ne 0) { throw "BatchBuilder failed (exit $LASTEXITCODE). See the tsep-*.log in $dist" }

if (Test-Path $output) {
    Write-Host "`nSUCCESS: $output" -ForegroundColor Green
    Write-Host ("Size: {0:N0} KB" -f ((Get-Item $output).Length / 1KB))
} else {
    throw "BatchBuilder reported success but $output was not created. Check the tsep-*.log in $dist"
}
