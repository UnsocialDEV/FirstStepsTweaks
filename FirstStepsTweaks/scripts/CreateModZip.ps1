param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectDir,

    [Parameter(Mandatory = $true)]
    [string]$TargetPath,

    [Parameter(Mandatory = $true)]
    [string]$ModName,

    [Parameter(Mandatory = $true)]
    [string]$ModsFolder
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $TargetPath)) {
    throw "TargetPath not found: $TargetPath"
}

$targetDir = Split-Path -Path $TargetPath -Parent
if (-not (Test-Path -LiteralPath $targetDir)) {
    throw "Build output directory not found: $targetDir"
}

$stagingRoot = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath ("{0}-staging" -f $ModName)
if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $stagingRoot | Out-Null

# Copy output files except external API references that should not be bundled.
Get-ChildItem -LiteralPath $targetDir -File | ForEach-Object {
    if ($_.Name -ieq 'VintagestoryAPI.dll') {
        return
    }

    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path -Path $stagingRoot -ChildPath $_.Name) -Force
}

$stagingAssetsPath = Join-Path -Path $stagingRoot -ChildPath 'assets'
$projectAssetsPath = Join-Path -Path $ProjectDir -ChildPath 'assets'
$assetsSourcePath = $null

# Prefer source-controlled assets so packaging is deterministic across OS/build targets.
if (Test-Path -LiteralPath $projectAssetsPath) {
    $assetsSourcePath = $projectAssetsPath
}
else {
    foreach ($candidateName in @('assets', 'Assets')) {
        $candidatePath = Join-Path -Path $targetDir -ChildPath $candidateName
        if (Test-Path -LiteralPath $candidatePath) {
            $assetsSourcePath = $candidatePath
            break
        }
    }
}

if ($assetsSourcePath -and (Test-Path -LiteralPath $assetsSourcePath)) {
    Get-ChildItem -LiteralPath $assetsSourcePath -File -Recurse | ForEach-Object {
        $relativePath = ($_.FullName.Substring($assetsSourcePath.Length) -replace '^[\\/]+', '')
        if ($relativePath.StartsWith("assets/", [System.StringComparison]::OrdinalIgnoreCase) -or $relativePath.StartsWith("assets\\", [System.StringComparison]::OrdinalIgnoreCase)) {
            return
        }

        $destinationPath = Join-Path -Path $stagingAssetsPath -ChildPath $relativePath
        $destinationDir = Split-Path -Path $destinationPath -Parent
        if (-not (Test-Path -LiteralPath $destinationDir)) {
            New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null
        }

        Copy-Item -LiteralPath $_.FullName -Destination $destinationPath -Force
    }
}

if (-not (Test-Path -LiteralPath (Join-Path -Path $stagingRoot -ChildPath 'modinfo.json'))) {
    $fallbackModInfo = Join-Path -Path $ProjectDir -ChildPath 'modinfo.json'
    if (Test-Path -LiteralPath $fallbackModInfo) {
        Copy-Item -LiteralPath $fallbackModInfo -Destination (Join-Path -Path $stagingRoot -ChildPath 'modinfo.json') -Force
    }
}

if (-not (Test-Path -LiteralPath $ModsFolder)) {
    New-Item -ItemType Directory -Path $ModsFolder -Force | Out-Null
}

$zipPath = Join-Path -Path $ModsFolder -ChildPath ("{0}.zip" -f $ModName)
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$stagingRootFullPath = [System.IO.Path]::GetFullPath($stagingRoot)
$zipArchive = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $stagingRootFullPath -File -Recurse | ForEach-Object {
        $relativePath = $_.FullName.Substring($stagingRootFullPath.Length).TrimStart('\', '/')
        $zipEntryName = $relativePath -replace '\\', '/'

        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zipArchive,
            $_.FullName,
            $zipEntryName,
            [System.IO.Compression.CompressionLevel]::Optimal
        ) | Out-Null
    }
}
finally {
    $zipArchive.Dispose()
}

Write-Host "Created mod zip: $zipPath"

