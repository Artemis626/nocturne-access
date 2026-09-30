<#
.SYNOPSIS
    Copies the build references for Nocturne Access from a local game install into _dlls/.

.DESCRIPTION
    The mod compiles against MelonLoader's own assemblies and the Il2CppInterop proxy
    assemblies that MelonLoader generates on first launch. Those files belong to the game,
    to Unity or to MelonLoader, so they are never committed. This script copies them
    from your install into the gitignored _dlls/ folder.

    Run it once after installing, and again after a game or MelonLoader update.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\copy-references.ps1 -GamePath "E:\SteamLibrary\steamapps\common\smt3hd"
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$GamePath
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$dest = Join-Path $repoRoot '_dlls'
$loaderDir = Join-Path $GamePath 'MelonLoader\net6'
$proxyDir = Join-Path $GamePath 'MelonLoader\Il2CppAssemblies'

if (-not (Test-Path (Join-Path $loaderDir 'MelonLoader.dll'))) {
    throw "MelonLoader net6 files not found in '$loaderDir'. Is MelonLoader 0.6.1 installed?"
}
if (-not (Test-Path (Join-Path $proxyDir 'Assembly-CSharp.dll'))) {
    throw "Proxy assemblies not found in '$proxyDir'. Launch the game once with MelonLoader to generate them."
}

New-Item -ItemType Directory -Force -Path $dest | Out-Null

# MelonLoader runtime pieces the mod compiles against.
$loaderFiles = @(
    'MelonLoader.dll',
    '0Harmony.dll',
    'Il2CppInterop.Runtime.dll',
    'Il2CppInterop.Common.dll'
)
foreach ($f in $loaderFiles) {
    Copy-Item -Force (Join-Path $loaderDir $f) $dest
}

# Every generated proxy assembly (game code, Unity modules, TextMeshPro, Il2Cpp corlib).
Copy-Item -Force (Join-Path $proxyDir '*.dll') $dest

$count = (Get-ChildItem $dest -Filter *.dll).Count
Write-Host "Copied $count reference assemblies to $dest"
