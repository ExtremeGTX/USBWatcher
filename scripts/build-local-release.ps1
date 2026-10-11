[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$')]
    [string]$ReleaseTag
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$velopackVersion = '1.2.161'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$outputRoot = Join-Path $repoRoot 'artifacts\local-release'
$expectedOutputRoot = Join-Path $repoRoot 'artifacts\local-release'

if ($outputRoot -ne $expectedOutputRoot) {
    throw "Unexpected output directory: $outputRoot"
}

if ($ReleaseTag -notmatch '^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') {
    throw 'ReleaseTag must use the format vX.Y.Z.A'
}

$winUiVersion = "$($Matches[1]).$($Matches[2])"
$winFormsVersion = "$($Matches[3]).$($Matches[4])"
$localSuffix = Get-Date -Format 'yyyyMMddHHmmss'
$winUiPackageVersion = "$winUiVersion.0-local.$localSuffix"
$winFormsPackageVersion = "$winFormsVersion.0-local.$localSuffix"

if (Test-Path -LiteralPath $outputRoot) {
    Remove-Item -LiteralPath $outputRoot -Recurse -Force
}

$toolDirectory = Join-Path $outputRoot '.tools'
$winUiPublish = Join-Path $outputRoot 'publish\winui'
$winFormsPublish = Join-Path $outputRoot 'publish\winforms'
$winUiVelopack = Join-Path $outputRoot 'velopack\winui'
$winFormsVelopack = Join-Path $outputRoot 'velopack\winforms'
$releaseDirectory = Join-Path $outputRoot 'release'

New-Item -ItemType Directory -Path $toolDirectory, $winUiPublish, $winFormsPublish,
    $winUiVelopack, $winFormsVelopack, $releaseDirectory -Force | Out-Null

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Executable,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Executable failed with exit code $LASTEXITCODE"
    }
}

Push-Location $repoRoot
try {
    Invoke-Checked dotnet @(
        'tool', 'install', 'vpk',
        '--tool-path', $toolDirectory,
        '--version', $velopackVersion
    )

    Invoke-Checked dotnet @(
        'restore', 'src/USBWatcher.WinUI/USBWatcher.WinUI.csproj',
        '--runtime', 'win-x64'
    )
    Invoke-Checked dotnet @(
        'restore', 'src/USBWatcher.WinForms/USBWatcher.csproj',
        '--runtime', 'win-x64'
    )

    Invoke-Checked dotnet @(
        'publish', 'src/USBWatcher.WinUI/USBWatcher.WinUI.csproj',
        '--configuration', 'Release',
        '--runtime', 'win-x64',
        '--self-contained', 'false',
        '--no-restore',
        '--output', $winUiPublish,
        "-p:Version=$winUiVersion",
        "-p:AssemblyVersion=$winUiVersion.0.0",
        "-p:FileVersion=$winUiVersion.0.0",
        '-p:WindowsPackageType=None'
    )
    Invoke-Checked dotnet @(
        'publish', 'src/USBWatcher.WinForms/USBWatcher.csproj',
        '--configuration', 'Release',
        '--runtime', 'win-x64',
        '--self-contained', 'false',
        '--no-restore',
        '--output', $winFormsPublish,
        "-p:Version=$winFormsVersion",
        "-p:AssemblyVersion=$winFormsVersion.0.0",
        "-p:FileVersion=$winFormsVersion.0.0"
    )

    $winUiPri = Join-Path $winUiPublish 'USBWatcher.WinUI.pri'
    if (-not (Test-Path -LiteralPath $winUiPri)) {
        throw "WinUI publish is incomplete: $winUiPri was not generated"
    }

    Remove-Item -Path (Join-Path $winUiPublish '*.pdb') -Force -ErrorAction SilentlyContinue
    Remove-Item -Path (Join-Path $winFormsPublish '*.pdb') -Force -ErrorAction SilentlyContinue
    Compress-Archive -Path (Join-Path $winUiPublish '*') `
        -DestinationPath (Join-Path $releaseDirectory "USBWatcher_$winUiVersion.zip")
    Compress-Archive -Path (Join-Path $winFormsPublish '*') `
        -DestinationPath (Join-Path $releaseDirectory "USBWatcher_win32_$winFormsVersion.zip")

    $vpk = Join-Path $toolDirectory 'vpk.exe'
    Invoke-Checked $vpk @(
        'pack',
        '--packId', 'USBWatcher.WinUI',
        '--packVersion', $winUiPackageVersion,
        '--packDir', $winUiPublish,
        '--mainExe', 'USBWatcher.WinUI.exe',
        '--packTitle', 'USBWatcher',
        '--packAuthors', 'ExtremeGTX',
        '--channel', 'winui',
        '--runtime', 'win-x64',
        '--framework', 'net8-x64-desktop',
        '--icon', 'src/USBWatcher.WinUI/Assets/AppIcon.ico',
        '--outputDir', $winUiVelopack,
        '--noPortable'
    )
    Invoke-Checked $vpk @(
        'pack',
        '--packId', 'USBWatcher.WinForms',
        '--packVersion', $winFormsPackageVersion,
        '--packDir', $winFormsPublish,
        '--mainExe', 'USBWatcher.exe',
        '--packTitle', 'USBWatcher',
        '--packAuthors', 'ExtremeGTX',
        '--channel', 'winforms',
        '--runtime', 'win-x64',
        '--framework', 'net8-x64-desktop',
        '--icon', 'src/USBWatcher.WinForms/Resources/appicon.ico',
        '--outputDir', $winFormsVelopack,
        '--noPortable'
    )

    Copy-Item -LiteralPath (Join-Path $winUiVelopack 'USBWatcher.WinUI-winui-Setup.exe') `
        -Destination (Join-Path $releaseDirectory "USBWatcher_$($winUiVersion)_Setup.exe")
    Copy-Item -LiteralPath (Join-Path $winFormsVelopack 'USBWatcher.WinForms-winforms-Setup.exe') `
        -Destination (Join-Path $releaseDirectory "USBWatcher_win32_$($winFormsVersion)_Setup.exe")
}
finally {
    Pop-Location
}

Write-Host ''
Write-Host "Local packages created in: $releaseDirectory"
Get-ChildItem -LiteralPath $releaseDirectory -File | Select-Object Name, Length
Write-Host ''
Write-Host 'Run either Setup normally (not as administrator) to test installation.'
Write-Host 'Uninstall local test builds before installing the final release.'
