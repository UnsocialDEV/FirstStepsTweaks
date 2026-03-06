[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DllPath,

    [Parameter(Mandatory = $true)]
    [string]$ProjectDir,

    [Parameter(Mandatory = $true)]
    [string]$OutputZip
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$dllResolved = (Resolve-Path -Path $DllPath).Path
$projectDirResolved = (Resolve-Path -Path $ProjectDir).Path
$modInfoPath = Join-Path -Path $projectDirResolved -ChildPath 'modinfo.json'
$assetsRoot = Join-Path -Path $projectDirResolved -ChildPath 'assets'

if (-not (Test-Path -Path $modInfoPath -PathType Leaf)) {
    throw "Could not find modinfo.json at '$modInfoPath'."
}

if (-not (Test-Path -Path $assetsRoot -PathType Container)) {
    throw "Could not find assets directory at '$assetsRoot'."
}

$outputDir = Split-Path -Path $OutputZip -Parent
if ($outputDir -and -not (Test-Path -Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

if (Test-Path -Path $OutputZip -PathType Leaf) {
    Remove-Item -Path $OutputZip -Force
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$zip = [System.IO.Compression.ZipFile]::Open($OutputZip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
        $zip,
        $dllResolved,
        [System.IO.Path]::GetFileName($dllResolved),
        [System.IO.Compression.CompressionLevel]::Optimal
    ) | Out-Null

    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
        $zip,
        $modInfoPath,
        'modinfo.json',
        [System.IO.Compression.CompressionLevel]::Optimal
    ) | Out-Null

    $assetsRootResolved = (Resolve-Path -Path $assetsRoot).Path
    if ($assetsRootResolved.EndsWith('\\') -or $assetsRootResolved.EndsWith('/')) {
        $assetsRootResolved = $assetsRootResolved.Substring(0, $assetsRootResolved.Length - 1)
    }

    Get-ChildItem -Path $assetsRootResolved -Recurse -File | ForEach-Object {
        $filePath = (Resolve-Path -Path $_.FullName).Path
        $relative = $filePath.Substring($assetsRootResolved.Length).TrimStart([char]92, [char]47)
        $entryName = 'assets/' + ($relative -replace '\\', '/')

        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip,
            $filePath,
            $entryName,
            [System.IO.Compression.CompressionLevel]::Optimal
        ) | Out-Null
    }
}
finally {
    $zip.Dispose()
}

Write-Host "Packed mod archive: $OutputZip"
