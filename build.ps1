$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$srcDir = Join-Path $root "src"
$outDir = Join-Path $root "bin"

$framework = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
if (-not (Test-Path $framework)) {
    $framework = "C:\Windows\Microsoft.NET\Framework\v4.0.30319"
}

$csc = Join-Path $framework "csc.exe"
if (-not (Test-Path $csc)) {
    throw "C# compiler not found: $csc"
}

if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir | Out-Null
}

$sources = @(Get-ChildItem -Path $srcDir -Filter *.cs -Recurse | ForEach-Object { $_.FullName })
if ($sources.Count -eq 0) {
    throw "No source files found in $srcDir"
}

$refs = @(
    "System.dll",
    "System.Core.dll",
    "System.Xml.dll",
    "System.Drawing.dll",
    "System.Windows.Forms.dll",
    "System.Management.dll"
)

$cscArgs = @()
foreach ($ref in $refs) {
    $refPath = Join-Path $framework $ref
    if (Test-Path $refPath) {
        $cscArgs += "/r:$refPath"
    } else {
        $cscArgs += "/r:$ref"
    }
}

$cscArgs += "/target:winexe"
$cscArgs += "/platform:anycpu"
$cscArgs += "/optimize+"
$cscArgs += "/codepage:65001"
$cscArgs += "/win32manifest:$(Join-Path $root 'app.manifest')"
$cscArgs += "/win32icon:$(Join-Path $root 'app.ico')"
$cscArgs += "/out:$(Join-Path $outDir 'AutoFlow.exe')"
$cscArgs += $sources

Write-Host "Compiling with $csc ..."
& $csc $cscArgs
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE"
}

Copy-Item (Join-Path $root "app.config") (Join-Path $outDir "AutoFlow.exe.config") -Force
Copy-Item (Join-Path $root "app.manifest") (Join-Path $outDir "AutoFlow.exe.manifest") -Force
Copy-Item (Join-Path $root "AutoFlow.config.sample.xml") (Join-Path $outDir "AutoFlow.config.sample.xml") -Force

Write-Host ""
Write-Host "Build succeeded."
Write-Host "Output: $outDir\AutoFlow.exe"
