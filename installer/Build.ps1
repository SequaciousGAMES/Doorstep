$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root "artifacts\publish"
$redist = Join-Path $PSScriptRoot "redist"
$bootstrapper = Join-Path $redist "MicrosoftEdgeWebview2Setup.exe"
$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    throw "Inno Setup 6 is not installed. Install it, then run this script again."
}

if (Test-Path $publish) {
    Remove-Item $publish -Recurse -Force
}

dotnet publish (Join-Path $root "src\Doorstep\Doorstep.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $publish
if ($LASTEXITCODE -ne 0) {
    throw "Publish failed."
}

$files = @(Get-ChildItem $publish -Recurse -File | Where-Object { $_.Extension -notin ".pdb", ".xml" })
$payloadBytes = [int64]($files | Measure-Object Length -Sum).Sum
$entries = New-Object System.Collections.Generic.List[string]
foreach ($file in $files) {
    $relative = $file.FullName.Substring($publish.Length).TrimStart("\", "/")
    $entries.Add($relative.Replace("\", "/"))
}
if ($entries -notcontains "Doorstep.exe") {
    throw "The published build did not contain Doorstep.exe."
}

New-Item -ItemType Directory -Force -Path (Join-Path $root "artifacts") | Out-Null
$zip = Join-Path $root "artifacts\Doorstep-win-x64.zip"
if (Test-Path $zip) {
    Remove-Item $zip -Force
}
foreach ($assembly in @("System.IO.Compression", "System.IO.Compression.FileSystem")) {
    try {
        Add-Type -AssemblyName $assembly -ErrorAction Stop
    }
    catch {
    }
}
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($relative in $entries) {
        $source = Join-Path $publish ($relative.Replace("/", "\"))
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive,
            $source,
            $relative,
            [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $archive.Dispose()
}

New-Item -ItemType Directory -Force -Path $redist | Out-Null
if (-not (Test-Path $bootstrapper)) {
    Write-Host "Downloading the WebView2 bootstrapper"
    Invoke-WebRequest -Uri "https://go.microsoft.com/fwlink/p/?LinkId=2124703" -OutFile $bootstrapper
}

& $iscc "/DPayloadBytes=$payloadBytes" (Join-Path $PSScriptRoot "Doorstep.iss")
if ($LASTEXITCODE -ne 0) {
    throw "Installer compile failed."
}

Write-Host "Package: $zip ($payloadBytes bytes uncompressed)"
Write-Host "Installer: $(Join-Path $root 'artifacts\DoorstepSetup.exe')"
