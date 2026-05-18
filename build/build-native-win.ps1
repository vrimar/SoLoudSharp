#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds soloud.dll for win-x64 (MSVC) with the miniaudio backend.

.DESCRIPTION
    Invokes CMake on build/CMakeLists.txt, builds Release x64, then stages
    the produced soloud.dll into artifacts/native/win-x64/.

.PARAMETER Configuration
    Release (default) or Debug. SoLoudSharp ships Release only.
#>
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',

    # Pin a specific Visual Studio generator if you have multiple installed.
    # Empty = let CMake auto-detect the newest installed VS.
    [string]$Generator = ''
)
$ErrorActionPreference = 'Stop'
$repo = Resolve-Path "$PSScriptRoot/.."
$soloud = Join-Path $repo 'external/soloud'

if (!(Test-Path (Join-Path $soloud 'include/soloud.h'))) {
    Write-Error "SoLoud submodule missing at $soloud. Run bootstrap.ps1 first."
}

# If no generator was specified, pick the newest installed VS that CMake supports.
if (-not $Generator) {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        # installationVersion is "18.6.11806.211" (VS 2026), "17.x" (VS 2022), "16.x" (VS 2019).
        $installVer = (& $vswhere -latest -prerelease -property installationVersion 2>$null)
        $major = if ($installVer) { ($installVer -split '\.')[0] } else { '' }
        switch ($major) {
            '18' { $Generator = 'Visual Studio 18 2026' }
            '17' { $Generator = 'Visual Studio 17 2022' }
            '16' { $Generator = 'Visual Studio 16 2019' }
            default { $Generator = 'Visual Studio 17 2022' }
        }
    }
    else {
        $Generator = 'Visual Studio 17 2022'
    }
}

$buildDir = Join-Path $repo 'build/build-win-x64'
if (Test-Path $buildDir) {
    Remove-Item -Recurse -Force $buildDir
}
New-Item -ItemType Directory -Path $buildDir -Force | Out-Null

Write-Host "[build-native-win] cmake -S build -B $buildDir -G `"$Generator`" -A x64"
& cmake -S (Join-Path $repo 'build') -B $buildDir -G $Generator -A x64
if ($LASTEXITCODE -ne 0) { Write-Error "cmake configure failed: $LASTEXITCODE" }

Write-Host "[build-native-win] cmake --build $buildDir --config $Configuration --parallel"
& cmake --build $buildDir --config $Configuration --parallel
if ($LASTEXITCODE -ne 0) { Write-Error "cmake --build failed: $LASTEXITCODE" }

$dll = Get-ChildItem -Path $buildDir -Filter 'soloud.dll' -Recurse | Select-Object -First 1
if (-not $dll) { Write-Error "soloud.dll not produced; inspect $buildDir." }

$nativeOut = Join-Path $repo 'artifacts/native/win-x64'
New-Item -ItemType Directory -Path $nativeOut -Force | Out-Null
Copy-Item -Path $dll.FullName -Destination (Join-Path $nativeOut 'soloud.dll') -Force

$pdb = Get-ChildItem -Path $buildDir -Filter 'soloud.pdb' -Recurse | Select-Object -First 1
if ($pdb) {
    Copy-Item -Path $pdb.FullName -Destination (Join-Path $nativeOut 'soloud.pdb') -Force
}

Write-Host "[build-native-win] Staged win-x64 artifacts:"
Get-ChildItem $nativeOut | Format-Table Name, Length

$dumpbin = (Get-Command dumpbin.exe -ErrorAction SilentlyContinue)?.Path
if ($dumpbin) {
    $exports = & $dumpbin /exports (Join-Path $nativeOut 'soloud.dll')
    if (-not ($exports -match '\bSoloud_create\b')) {
        Write-Error "soloud.dll does not export Soloud_create - build is broken."
    }
    Write-Host "[build-native-win] symbol check OK: Soloud_create exported."
}
else {
    Write-Warning "dumpbin.exe not in PATH; skipping symbol-export check."
}

$global:LASTEXITCODE = 0
exit 0
