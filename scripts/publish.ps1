param(
    [ValidatePattern('^\d+\.\d+(\.\d+)?([-.][0-9A-Za-z.-]+)?$')]
    [string]$Version = '1.1',

    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectFile = Join-Path $projectRoot 'DeltaruneMidiPlayer.csproj'
$artifactsRoot = Join-Path $projectRoot 'artifacts'
$packageName = "DeltaruneMidiPlayer-v$Version-win-x64"
$publishDirectory = Join-Path $artifactsRoot $packageName
$archivePath = Join-Path $artifactsRoot "$packageName.zip"

if (-not (Test-Path -LiteralPath $projectFile -PathType Leaf)) {
    throw "Project file not found: $projectFile"
}

New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null
$resolvedArtifactsRoot = [System.IO.Path]::GetFullPath($artifactsRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
$resolvedPublishDirectory = [System.IO.Path]::GetFullPath($publishDirectory)
$expectedPrefix = $resolvedArtifactsRoot + [System.IO.Path]::DirectorySeparatorChar

if (-not $resolvedPublishDirectory.StartsWith($expectedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe publish directory: $resolvedPublishDirectory"
}

if (Test-Path -LiteralPath $resolvedPublishDirectory) {
    $target = Get-Item -LiteralPath $resolvedPublishDirectory -Force
    if (-not $target.PSIsContainer -or ($target.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
        throw "Publish target must be a regular directory: $resolvedPublishDirectory"
    }
    Remove-Item -LiteralPath $resolvedPublishDirectory -Recurse -Force
}

$publishArguments = @(
    'publish',
    $projectFile,
    '--configuration', 'Release',
    '--runtime', 'win-x64',
    '--self-contained', 'true',
    '--output', $resolvedPublishDirectory,
    "-p:Version=$Version"
)
if ($NoRestore) {
    $publishArguments += '--no-restore'
}

& dotnet @publishArguments

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

if (Test-Path -LiteralPath $archivePath -PathType Leaf) {
    Remove-Item -LiteralPath $archivePath -Force
}

Compress-Archive -Path (Join-Path $resolvedPublishDirectory '*') -DestinationPath $archivePath -CompressionLevel Optimal

Write-Host "Release directory: $resolvedPublishDirectory"
Write-Host "Release archive:   $archivePath"
